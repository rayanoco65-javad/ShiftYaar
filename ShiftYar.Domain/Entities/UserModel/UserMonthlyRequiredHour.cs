using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.UserModel
{
    /// <summary>
    /// ساعت موظفی ماهانه پرسنل برای یک بخش و ماه شمسی مشخص.
    /// سوپروایزر قبل از شیفت‌بندی این ساعت‌ها را در یک پنجره متمرکز بررسی، تأیید یا اصلاح می‌کند.
    /// </summary>
    public class UserMonthlyRequiredHour : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User? User { get; set; }

        [ForeignKey(nameof(Department))]
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        /// <summary>سال شمسی (مثلاً ۱۴۰۵)</summary>
        public int PersianYear { get; set; }

        /// <summary>ماه شمسی (۱ تا ۱۲)</summary>
        public int PersianMonth { get; set; }

        /// <summary>ساعت موظفی محاسبه‌شده توسط الگوریتم سیستم</summary>
        public decimal CalculatedHours { get; set; }

        /// <summary>ساعت موظفی تأیید/اصلاح‌شده توسط سوپروایزر (ملاک نهایی شیفت‌بندی)</summary>
        public decimal ApprovedHours { get; set; }

        /// <summary>آیا مقدار توسط سوپروایزر دستی تغییر یافته است؟</summary>
        public bool IsManuallyEdited { get; set; }

        /// <summary>توضیحات یا یادداشت سوپروایزر در صورت ویرایش (مثلاً کسر ساعت شیردهی/استعلاجی)</summary>
        public string? Notes { get; set; }

        /// <summary>تاریخ و زمان ثبت یا آخرین تأیید</summary>
        public DateTime? ConfirmedAt { get; set; }

        /// <summary>شناسه کاربری سوپروایزر تأییدکننده</summary>
        public int? ConfirmedByUserId { get; set; }
    }
}
