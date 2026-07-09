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

            while (!stoppingToken.IsCancellationRequested)
            {
                string jobId;
                try
                {
                    jobId = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // در حال خاموش شدن برنامه
                }

                await ProcessJobAsync(jobId, stoppingToken);
            }

            _logger.LogInformation("SchedulingBackgroundService stopping.");
        }

        private async Task ProcessJobAsync(string jobId, CancellationToken stoppingToken)
        {
            var job = await _store.GetAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Scheduling job {JobId} not found in store; skipping.", jobId);
                return;
            }

            job.Status = SchedulingJobStatus.Running;
            job.StartedAtUtc = DateTime.UtcNow;
            await _store.UpdateAsync(job);

            // هر کار در یک scope مستقل اجرا می‌شود (DbContext و سرویس‌های scoped جداگانه).
            using var scope = _scopeFactory.CreateScope();
            var schedulingService = scope.ServiceProvider.GetRequiredService<IShiftSchedulingService>();

            try
            {
                _logger.LogInformation(
                    "Running scheduling job {JobId} for DepartmentId={DepartmentId}, Algorithm={Algorithm}",
                    job.Id, job.Request.DepartmentId, job.Request.Algorithm);

                var result = await schedulingService.OptimizeAndSaveAsync(job.Request);

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
                job.CompletedAtUtc = DateTime.UtcNow;
                await _store.UpdateAsync(job);
            }
        }
    }
}
