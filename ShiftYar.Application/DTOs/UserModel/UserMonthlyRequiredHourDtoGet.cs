using System;

namespace ShiftYar.Application.DTOs.UserModel
{
    /// <summary>
    /// خروجی اطلاعات ساعت موظفی ماهانه پرسنل
    /// </summary>
    public class UserMonthlyRequiredHourDtoGet
    {
        public int? Id { get; set; }
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string? PersonnelCode { get; set; }
        public string? JobTitle { get; set; }
        public string? ShiftType { get; set; }
        public string? ShiftSubType { get; set; }
        public int DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public int PersianYear { get; set; }
        public int PersianMonth { get; set; }

        /// <summary>ساعت موظفی محاسبه‌شده بر اساس تقویم و قانون ارتقای بهره‌وری</summary>
        public decimal CalculatedHours { get; set; }

        /// <summary>ساعت موظفی تأیید/اصلاح‌شده نهایی که ملاک شیفت‌بندی است</summary>
        public decimal ApprovedHours { get; set; }

        /// <summary>آیا قبلاً در دیتابیس تأیید و ذخیره شده یا صرفاً پیشنهاد اولیه سیستم است</summary>
        public bool IsSaved { get; set; }

        /// <summary>آیا توسط سوپروایزر دستی اصلاح شده است</summary>
        public bool IsManuallyEdited { get; set; }

        /// <summary>توضیحات اختیاری سوپروایزر</summary>
        public string? Notes { get; set; }

        public DateTime? ConfirmedAt { get; set; }
        public int? ConfirmedByUserId { get; set; }
        public string? ConfirmedByUserName { get; set; }

        // فیلدهای کمکی جهت شفافیت برای سوپروایزر
        public int? YearsOfService { get; set; }
        public decimal? HardshipPercent { get; set; }
        public decimal? WeeklyReductionHours { get; set; }
        public int? WorkingDaysCount { get; set; }
    }
}
