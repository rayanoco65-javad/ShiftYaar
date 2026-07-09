using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShiftYar.Application.Common.Utilities;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel
{
    /// <summary>
    /// درخواست بهینه‌سازی شیفت‌بندی
    /// </summary>
    public class ShiftSchedulingRequestDto : IValidatableObject // درخواست اجرای زمان‌بندی شیفت
    {
        [Range(1, int.MaxValue, ErrorMessage = "شناسه دپارتمان باید مقدار مثبت داشته باشد.")]
        public int DepartmentId { get; set; } // شناسه دپارتمان هدف

        [Required(ErrorMessage = "تاریخ شروع الزامی است.")]
        [RegularExpression(@"^\d{4}/\d{2}/\d{2}$", ErrorMessage = "تاریخ شروع باید در قالب yyyy/MM/dd باشد.")]
        public string StartDate { get; set; } = string.Empty; // تاریخ شروع بازه (شمسی)

        [Required(ErrorMessage = "تاریخ پایان الزامی است.")]
        [RegularExpression(@"^\d{4}/\d{2}/\d{2}$", ErrorMessage = "تاریخ پایان باید در قالب yyyy/MM/dd باشد.")]
        public string EndDate { get; set; } = string.Empty; // تاریخ پایان بازه (شمسی)
        public SchedulingAlgorithm Algorithm { get; set; } = SchedulingAlgorithm.SimulatedAnnealing; // الگوریتم انتخابی

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!TryParsePersianDate(StartDate, nameof(StartDate), out var start, out var startError))
            {
                yield return startError;
                yield break;
            }

            if (!TryParsePersianDate(EndDate, nameof(EndDate), out var end, out var endError))
            {
                yield return endError;
                yield break;
            }

            if (end < start)
            {
                yield return new ValidationResult("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.", new[] { nameof(EndDate) });
            }

            var totalDays = (end - start).TotalDays + 1;
            if (totalDays <= 0)
            {
                yield return new ValidationResult("بازه تاریخ باید حداقل یک روز باشد.", new[] { nameof(StartDate), nameof(EndDate) });
            }
            else if (totalDays > 62)
            {
                yield return new ValidationResult("طول بازه زمان‌بندی نمی‌تواند بیش از 62 روز باشد.", new[] { nameof(EndDate) });
            }
        }

        private static bool TryParsePersianDate(string value, string fieldName, out DateTime date, out ValidationResult error)
        {
            try
            {
                date = DateConverter.ConvertToGregorianDate(value);
                error = ValidationResult.Success!;
                return true;
            }
            catch
            {
                date = default;
                error = new ValidationResult($"فرمت {fieldName} صحیح نیست.", new[] { fieldName });
                return false;
            }
        }
    }

    /// <summary>
    /// محدودیت کاربر
    /// </summary>
    public class UserConstraintDto // محدودیت‌های یک کاربر در درخواست
    {
        public int UserId { get; set; } // شناسه کاربر

        //public List<DateTime> UnavailableDates { get; set; } = new List<DateTime>(); // تاریخ‌های عدم حضور
        public List<string> UnavailableDates { get; set; } = new List<string>(); // تاریخ‌های عدم حضور (شمسی)

        public List<ShiftLabel> PreferredShifts { get; set; } = new List<ShiftLabel>(); // شیفت‌های ترجیحی
        public List<ShiftLabel> UnwantedShifts { get; set; } = new List<ShiftLabel>(); // شیفت‌های ناخواسته
        public int MaxConsecutiveShifts { get; set; } = 3; // سقف شیفت متوالی
        public int MinRestDaysBetweenShifts { get; set; } = 1; // حداقل روز استراحت
        public int MaxShiftsPerWeek { get; set; } = 5; // سقف شیفت هفتگی
        public int MaxNightShiftsPerMonth { get; set; } = 8; // سقف شیفت شب ماهانه
    }

    // پارامترهای الگوریتم از دیتابیس خوانده می‌شوند؛ نیازی به ارسال در درخواست نیست.

    /// <summary>
    /// الگوریتم‌های زمان‌بندی
    /// </summary>
    public enum SchedulingAlgorithm // انتخاب نوع الگوریتم
    {
        SimulatedAnnealing = 1,
        OrToolsCPSat = 2,
        Hybrid = 3
    }

    /// <summary>
    /// استراتژی‌های الگوریتم ترکیبی
    /// </summary>
    public enum HybridStrategy // استراتژی‌های Hybrid در DTO
    {
        OrToolsFirst = 1,
        SimulatedAnnealingFirst = 2,
        Parallel = 3,
        Iterative = 4,
        Adaptive = 5
    }
}