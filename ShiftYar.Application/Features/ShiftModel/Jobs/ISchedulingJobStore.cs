using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;

namespace ShiftYar.Application.Features.ShiftModel.Jobs
{
    /// <summary>
    /// نگهدارندهٔ پایدار وضعیت کارهای زمان‌بندی پس‌زمینه (روی دیتابیس).
    /// </summary>
    public interface ISchedulingJobStore
    {
        /// ایجاد یک کار جدید در وضعیت Queued و برگرداندن آن
        Task<SchedulingJob> CreateAsync(ShiftSchedulingRequestDto request);

        /// دریافت یک کار بر اساس شناسه (در صورت نبود، null)
        Task<SchedulingJob> GetAsync(string id);

        /// به‌روزرسانی وضعیت/نتیجهٔ یک کار
        Task UpdateAsync(SchedulingJob job);

        /// کارهای Running که بیش از آستانه در حال اجرا بوده‌اند را Failed می‌کند (بازیابی پس از کرش/ری‌استارت).
        Task<int> MarkStaleRunningJobsAsFailedAsync(TimeSpan staleThreshold);

        /// کارهای Queued که هرگز شروع نشده‌اند (مثلاً پس از ری‌استارت بدون dequeue) را Failed می‌کند.
        Task<int> MarkStaleQueuedJobsAsFailedAsync(TimeSpan staleThreshold);

        /// شناسهٔ کارهای Queued ذخیره‌شده در DB (برای بازیابی صف پس از ری‌استارت).
        Task<IReadOnlyList<string>> GetQueuedJobIdsAsync();
    }
}
