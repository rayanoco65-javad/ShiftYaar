using System;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;

namespace ShiftYar.Application.Features.ShiftModel.Jobs
{
    /// <summary>
    /// وضعیت اجرای یک کار زمان‌بندی پس‌زمینه
    /// </summary>
    public enum SchedulingJobStatus
    {
        Queued = 0,   // در صف
        Running = 1,  // در حال اجرا
        Succeeded = 2, // موفق
        Failed = 3    // ناموفق
    }

    /// <summary>
    /// یک کار زمان‌بندی که به‌صورت پس‌زمینه (خارج از درخواست HTTP) اجرا می‌شود.
    /// این کار جلوی 502 ناشی از اجرای طولانی/سنگین الگوریتم‌ها را در مسیر درخواست می‌گیرد.
    /// </summary>
    public class SchedulingJob
    {
        public string Id { get; set; }
        public SchedulingJobStatus Status { get; set; } = SchedulingJobStatus.Queued;
        public ShiftSchedulingRequestDto Request { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        // خروجی اجرا
        public bool? IsSuccess { get; set; }
        public string Message { get; set; }
        public object Result { get; set; }
    }
}
