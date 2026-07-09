using ShiftYar.Domain.Entities.BaseModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Domain.Entities.ShiftModel
{
    /// <summary>
    /// وضعیت پایدار یک کار زمان‌بندی پس‌زمینه.
    /// در دیتابیس ذخیره می‌شود تا در برابر ری‌استارت/کرش پراسس و اجرای چند-اینستنسی مقاوم باشد
    /// (برخلاف نگهداری در حافظه که با ری‌استارت از بین می‌رفت و «job not found» می‌داد).
    /// </summary>
    public class SchedulingJobRecord : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        // شناسهٔ عمومی کار (GUID) که به کلاینت داده می‌شود
        [MaxLength(64)]
        public string JobId { get; set; }

        // معادل عددی SchedulingJobStatus
        public int Status { get; set; }

        public int DepartmentId { get; set; }

        // ورودی درخواست به‌صورت JSON (برای اجرای پس‌زمینه)
        public string RequestJson { get; set; }

        // نتیجهٔ اجرا به‌صورت JSON (در صورت موفقیت)
        public string ResultJson { get; set; }

        public bool? IsSuccess { get; set; }

        public string Message { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }
}
