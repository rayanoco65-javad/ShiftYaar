using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.ShiftModel;

namespace ShiftYar.Application.Features.ShiftModel.Jobs
{
    /// <summary>
    /// نگهدارندهٔ وضعیت کارها روی دیتابیس (پایدار در برابر ری‌استارت/کرش و چند-اینستنسی).
    /// از الگوی مخزن موجود (IEfRepository) استفاده می‌کند. چون این کلاس Singleton است،
    /// برای هر عملیات یک scope مستقل ساخته می‌شود تا DbContext (که Scoped است) درست مدیریت شود.
    /// </summary>
    public class SchedulingJobStore : ISchedulingJobStore
    {
        private readonly IServiceScopeFactory _scopeFactory;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public SchedulingJobStore(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<SchedulingJob> CreateAsync(ShiftSchedulingRequestDto request)
        {
            var job = new SchedulingJob
            {
                Id = Guid.NewGuid().ToString("N"),
                Status = SchedulingJobStatus.Queued,
                Request = request,
                CreatedAtUtc = DateTime.UtcNow
            };

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEfRepository<SchedulingJobRecord>>();

            var record = new SchedulingJobRecord
            {
                JobId = job.Id,
                Status = (int)job.Status,
                DepartmentId = request.DepartmentId,
                RequestJson = JsonSerializer.Serialize(request, JsonOptions),
                CreatedAtUtc = job.CreatedAtUtc
            };

            await repo.AddAsync(record);
            await repo.SaveAsync();

            return job;
        }

        public async Task<SchedulingJob> GetAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEfRepository<SchedulingJobRecord>>();

            var (items, _) = await repo.GetByFilterAsync(
                new SimpleFilter<SchedulingJobRecord>(r => r.JobId == id));

            var record = items.FirstOrDefault();
            return record == null ? null : ToJob(record);
        }

        public async Task UpdateAsync(SchedulingJob job)
        {
            if (job == null || string.IsNullOrEmpty(job.Id)) return;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEfRepository<SchedulingJobRecord>>();

            var (items, _) = await repo.GetByFilterAsync(
                new SimpleFilter<SchedulingJobRecord>(r => r.JobId == job.Id));

            var record = items.FirstOrDefault();
            if (record == null) return;

            record.Status = (int)job.Status;
            record.IsSuccess = job.IsSuccess;
            record.Message = job.Message;
            record.ResultJson = job.Result == null ? null : JsonSerializer.Serialize(job.Result, JsonOptions);
            record.StartedAtUtc = job.StartedAtUtc;
            record.CompletedAtUtc = job.CompletedAtUtc;
            record.UpdateDate = DateTime.UtcNow;

            repo.Update(record);
            await repo.SaveAsync();
        }

        public async Task<int> MarkStaleRunningJobsAsFailedAsync(TimeSpan staleThreshold)
        {
            var cutoff = DateTime.UtcNow - staleThreshold;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEfRepository<SchedulingJobRecord>>();

            var (items, _) = await repo.GetByFilterAsync(
                new SimpleFilter<SchedulingJobRecord>(r =>
                    r.Status == (int)SchedulingJobStatus.Running &&
                    (
                        (r.UpdateDate ?? r.StartedAtUtc ?? DateTime.MaxValue) < cutoff
                    )));

            if (items.Count == 0)
            {
                return 0;
            }

            foreach (var record in items)
            {
                record.Status = (int)SchedulingJobStatus.Failed;
                record.IsSuccess = false;
                record.Message = "Job was interrupted or timed out while running. Please submit a new job.";
                record.CompletedAtUtc = DateTime.UtcNow;
                record.UpdateDate = DateTime.UtcNow;
                repo.Update(record);
            }

            await repo.SaveAsync();
            return items.Count;
        }

        public async Task<int> MarkStaleQueuedJobsAsFailedAsync(TimeSpan staleThreshold)
        {
            var cutoff = DateTime.UtcNow - staleThreshold;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEfRepository<SchedulingJobRecord>>();

            var (items, _) = await repo.GetByFilterAsync(
                new SimpleFilter<SchedulingJobRecord>(r =>
                    r.Status == (int)SchedulingJobStatus.Queued &&
                    r.CreatedAtUtc < cutoff));

            if (items.Count == 0)
            {
                return 0;
            }

            foreach (var record in items)
            {
                record.Status = (int)SchedulingJobStatus.Failed;
                record.IsSuccess = false;
                record.Message =
                    "Job remained queued too long without starting. Restart the API or submit a new job.";
                record.CompletedAtUtc = DateTime.UtcNow;
                record.UpdateDate = DateTime.UtcNow;
                repo.Update(record);
            }

            await repo.SaveAsync();
            return items.Count;
        }

        public async Task<IReadOnlyList<string>> GetQueuedJobIdsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEfRepository<SchedulingJobRecord>>();

            var (items, _) = await repo.GetByFilterAsync(
                new SimpleFilter<SchedulingJobRecord>(r => r.Status == (int)SchedulingJobStatus.Queued));

            return items
                .OrderBy(r => r.CreatedAtUtc)
                .Select(r => r.JobId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }

        private static SchedulingJob ToJob(SchedulingJobRecord record)
        {
            ShiftSchedulingRequestDto request = null;
            if (!string.IsNullOrEmpty(record.RequestJson))
            {
                try { request = JsonSerializer.Deserialize<ShiftSchedulingRequestDto>(record.RequestJson, JsonOptions); }
                catch { /* ورودی نامعتبر ذخیره‌شده را نادیده می‌گیریم */ }
            }

            object result = null;
            if (!string.IsNullOrEmpty(record.ResultJson))
            {
                try { result = JsonSerializer.Deserialize<JsonElement>(record.ResultJson, JsonOptions); }
                catch { /* نتیجهٔ نامعتبر ذخیره‌شده را نادیده می‌گیریم */ }
            }

            return new SchedulingJob
            {
                Id = record.JobId,
                Status = (SchedulingJobStatus)record.Status,
                Request = request,
                IsSuccess = record.IsSuccess,
                Message = record.Message,
                Result = result,
                CreatedAtUtc = record.CreatedAtUtc,
                StartedAtUtc = record.StartedAtUtc,
                CompletedAtUtc = record.CompletedAtUtc
            };
        }
    }
}
