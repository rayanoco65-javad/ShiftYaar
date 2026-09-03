using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Interfaces.ShiftModel;

namespace ShiftYar.Application.Features.ShiftModel.Jobs
{
    /// <summary>
    /// سرویس پس‌زمینه‌ای که کارهای زمان‌بندی را از صف برداشته و خارج از درخواست HTTP اجرا می‌کند.
    /// حل سنگین OR-Tools/Hybrid دیگر thread درخواست را بلاک نمی‌کند، پس 502 (timeout پروکسی) رخ نمی‌دهد.
    /// </summary>
    public class SchedulingBackgroundService : BackgroundService
    {
        private static readonly TimeSpan StaleJobThreshold = TimeSpan.FromMinutes(35);
        private static readonly TimeSpan StaleQueuedJobThreshold = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan JobExecutionTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan StaleCheckInterval = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan QueuedRecoveryInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(2);

        private readonly ISchedulingJobQueue _queue;
        private readonly ISchedulingJobStore _store;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SchedulingBackgroundService> _logger;

        public SchedulingBackgroundService(
            ISchedulingJobQueue queue,
            ISchedulingJobStore store,
            IServiceScopeFactory scopeFactory,
            ILogger<SchedulingBackgroundService> logger)
        {
            _queue = queue;
            _store = store;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SchedulingBackgroundService started.");

            await RecoverJobsOnStartupAsync(stoppingToken);

            var staleCheckTask = RunStaleCheckLoopAsync(stoppingToken);
            var queuedRecoveryTask = RunQueuedRecoveryLoopAsync(stoppingToken);
            var dequeueTask = DequeueLoopAsync(stoppingToken);

            await Task.WhenAll(staleCheckTask, queuedRecoveryTask, dequeueTask);

            _logger.LogInformation("SchedulingBackgroundService stopping.");
        }

        private async Task RunStaleCheckLoopAsync(CancellationToken stoppingToken)
        {
            using var staleCheckTimer = new PeriodicTimer(StaleCheckInterval);
            while (await staleCheckTimer.WaitForNextTickAsync(stoppingToken))
            {
                await MarkStaleJobsAsync();
            }
        }

        private async Task RecoverJobsOnStartupAsync(CancellationToken stoppingToken)
        {
            var recovered = await _store.MarkStaleRunningJobsAsFailedAsync(StaleJobThreshold);
            if (recovered > 0)
            {
                _logger.LogWarning("Marked {Count} stale scheduling job(s) as Failed on startup.", recovered);
            }

            var queuedJobIds = await _store.GetQueuedJobIdsAsync();
            foreach (var jobId in queuedJobIds)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await _queue.EnqueueAsync(jobId, stoppingToken);
                _logger.LogInformation("Re-queued scheduling job {JobId} from database after startup.", jobId);
            }
        }

        private async Task MarkStaleJobsAsync()
        {
            var staleRunning = await _store.MarkStaleRunningJobsAsFailedAsync(StaleJobThreshold);
            if (staleRunning > 0)
            {
                _logger.LogWarning("Marked {Count} stale scheduling job(s) as Failed during periodic check.", staleRunning);
            }

            var staleQueued = await _store.MarkStaleQueuedJobsAsFailedAsync(StaleQueuedJobThreshold);
            if (staleQueued > 0)
            {
                _logger.LogWarning("Marked {Count} orphaned Queued scheduling job(s) as Failed.", staleQueued);
            }
        }

        private async Task RunQueuedRecoveryLoopAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(QueuedRecoveryInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RecoverQueuedJobsAsync(stoppingToken);
            }
        }

        private async Task RecoverQueuedJobsAsync(CancellationToken stoppingToken)
        {
            var queuedJobIds = await _store.GetQueuedJobIdsAsync();
            foreach (var jobId in queuedJobIds)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await _queue.EnqueueAsync(jobId, stoppingToken);
            }
        }

        private async Task DequeueLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                string jobId;
                try
                {
                    jobId = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await ProcessJobAsync(jobId, stoppingToken);
            }
        }

        private async Task ProcessJobAsync(string jobId, CancellationToken stoppingToken)
        {
            var job = await _store.GetAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Scheduling job {JobId} not found in store; skipping.", jobId);
                return;
            }

            if (job.Request == null)
            {
                job.Status = SchedulingJobStatus.Failed;
                job.IsSuccess = false;
                job.Message = "Stored job request is invalid or missing.";
                job.CompletedAtUtc = DateTime.UtcNow;
                await _store.UpdateAsync(job);
                return;
            }

            if (job.Status != SchedulingJobStatus.Queued)
            {
                _logger.LogDebug(
                    "Scheduling job {JobId} skipped because status is {Status}.",
                    jobId, job.Status);
                return;
            }

            job.Status = SchedulingJobStatus.Running;
            job.StartedAtUtc = DateTime.UtcNow;
            job.Message = "Job started.";
            await _store.UpdateAsync(job);

            CancellationTokenSource heartbeatCts = null;
            Task heartbeatTask = Task.CompletedTask;

            try
            {
                _logger.LogInformation(
                    "Running scheduling job {JobId} for DepartmentId={DepartmentId}, Algorithm={Algorithm}",
                    job.Id, job.Request.DepartmentId, job.Request.Algorithm);

                heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                heartbeatTask = RunJobHeartbeatAsync(job.Id, heartbeatCts.Token);

                // LongRunning: OR-Tools/Hybrid از thread pool جدا می‌شود تا قفل thread pool (Task.WaitAll) رخ ندهد.
                var workTask = Task.Factory.StartNew(
                    () => ExecuteJobInScopeAsync(job.Id, job.Request),
                    stoppingToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();

                var completedTask = await Task.WhenAny(workTask, Task.Delay(JobExecutionTimeout, stoppingToken));

                if (completedTask != workTask)
                {
                    job.IsSuccess = false;
                    job.Message = $"Scheduling job timed out after {JobExecutionTimeout.TotalMinutes:0} minutes.";
                    job.Status = SchedulingJobStatus.Failed;
                    _logger.LogError(
                        "Scheduling job {JobId} timed out after {TimeoutMinutes} minutes.",
                        job.Id, JobExecutionTimeout.TotalMinutes);
                    return;
                }

                var result = await workTask;

                job.IsSuccess = result.IsSuccess;
                job.Message = result.Message;
                job.Result = result.Data;
                job.Status = result.IsSuccess ? SchedulingJobStatus.Succeeded : SchedulingJobStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduling job {JobId} failed with an unhandled exception.", job.Id);
                job.IsSuccess = false;
                job.Message = ex.Message;
                job.Status = SchedulingJobStatus.Failed;
            }
            finally
            {
                heartbeatCts?.Cancel();
                try
                {
                    await heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                    // expected when job completes
                }

                job.CompletedAtUtc = DateTime.UtcNow;
                try
                {
                    await _store.UpdateAsync(job);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Critical error updating database in finally block for job {JobId}.", job.Id);
                }
            }
        }

        private async Task RunJobHeartbeatAsync(string jobId, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(HeartbeatInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var job = await _store.GetAsync(jobId);
                if (job == null || job.Status != SchedulingJobStatus.Running)
                {
                    break;
                }

                job.Message = $"Still optimizing... (last update {DateTime.UtcNow:HH:mm:ss} UTC)";
                await _store.UpdateAsync(job);
            }
        }

        private async Task<Application.Common.Models.ResponseModel.ApiResponse<object>> ExecuteJobInScopeAsync(
            string jobId,
            Application.DTOs.ShiftModel.ShiftSchedulingModel.ShiftSchedulingRequestDto request)
        {
            using var scope = _scopeFactory.CreateScope();
            var schedulingService = scope.ServiceProvider.GetRequiredService<IShiftSchedulingService>();
            
            if (request.SaveAfterOptimize)
            {
                return await schedulingService.OptimizeAndSaveAsync(request, isBackgroundExecution: true, backgroundJobId: jobId);
            }
            else
            {
                var result = await schedulingService.OptimizeShiftScheduleAsync(request, default);
                return new Application.Common.Models.ResponseModel.ApiResponse<object>
                {
                    IsSuccess = result.IsSuccess,
                    Message = result.Message,
                    Data = result.Data
                };
            }
        }
    }
}
