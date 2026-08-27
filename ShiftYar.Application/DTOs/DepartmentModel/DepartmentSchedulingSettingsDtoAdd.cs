using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.DepartmentModel
{
    public class DepartmentSchedulingSettingsDtoAdd
    {
        [Required]
        public int DepartmentId { get; set; }

        //قوانین سخت
        public bool? ForbidDuplicateDailyAssignments { get; set; }  //ممنوعیت تکالیف تکراری روزانه
        public bool? EnforceMaxShiftsPerDay { get; set; }   //اعمال حداکثر شیفت در روز
        public bool? EnforceMinRestDays { get; set; }   //اعمال حداقل روزهای استراحت
        public bool? EnforceMaxConsecutiveShifts { get; set; }  //اعمال سقف روزهای کاری متوالی
        public bool? EnforceWeeklyMaxShifts { get; set; }   //اعمال حداکثر شیفت‌های هفتگی
        public bool? EnforceNightShiftMonthlyCap { get; set; }  //اعمال سقف ماهانه شیفت شب
        public bool? EnforceSpecialtyCapacity { get; set; } //اعمال ظرفیت تخصصی

        // مقادیر عددی قوانین سخت (اختیاری؛ هنگام فعال بودن Enforce ها اعمال می‌شوند)
        public int? MinRestDaysBetweenShifts { get; set; }  //حداقل روزهای استراحت بین شیفت‌ها
        /// <summary>حداکثر روز کاری متوالی (هر روز با حداقل یک شیفت). null/نامعتبر → پیش‌فرض ۳.</summary>
        public int? MaxConsecutiveShifts { get; set; }
        public int? MaxShiftsPerWeek { get; set; }  //حداکثر شیفت در هفته
        public int? MaxNightShiftsPerMonth { get; set; }    //حداکثر شیفت شب در ماه
        [Range(1, 2, ErrorMessage = "حداکثر شیفت در روز فقط می‌تواند ۱ یا ۲ باشد.")]
        public int? MaxShiftsPerDay { get; set; }   //حداکثر شیفت در روز
        public int? MaxConsecutiveNightShifts { get; set; } //حداکثر شیفت شب متوالی

        //قوانین نرم
        public double? GenderBalanceWeight { get; set; }    //وزن تعادل جنسیتی
        public double? SpecialtyPreferenceWeight { get; set; }  //وزن ترجیحی تخصصی
        public double? UserUnwantedShiftWeight { get; set; }    //وزن تغییر ناخواسته کاربر
        public double? UserPreferredShiftWeight { get; set; }   //وزن شیفت ترجیحی کاربر
        public double? WeeklyMaxWeight { get; set; }    //حداکثر وزن هفتگی
        public double? MonthlyNightCapWeight { get; set; }  //وزن کلاه شبانه ماهانه

        // عدالت و چرخش
        public double? FairShiftCountBalanceWeight { get; set; }    //وزن تعادل شمارش منصفانه شیفت
        public double? ExtraShiftRotationWeight { get; set; }   //وزن چرخش شیفت اضافی
        public double? ShiftLabelBalanceWeight { get; set; }    //تغییر وزن تعادل برچسب
        public int? FairnessLookbackMonths { get; set; }    //عادلانه نگاه کنید به ماه ها

        // تنظیمات حداقل شیفت برای انواع مختلف شیفت‌گردشی
        public bool? EnforceMinimumShiftsForRotatingStaff { get; set; } //اجرای حداقل شیفت برای کارکنان چرخشی
        public int? MinMorningShiftsForThreeShiftRotation { get; set; } //حداقل شیفت صبح برای چرخش سه شیفت
        public int? MinEveningShiftsForThreeShiftRotation { get; set; } //حداقل شیفت عصر برای چرخش سه شیفت
        public int? MinNightShiftsForThreeShiftRotation { get; set; }   //حداقل شیفت شب برای چرخش سه شیفت
        public int? MinFirstShiftForTwoShiftRotation { get; set; }  //حداقل شیفت اول برای چرخش دو شیفت
        public int? MinSecondShiftForTwoShiftRotation { get; set; } //حداقل شیفت دوم برای چرخش دو شیفت

        // تنظیمات شب‌دوست/شب‌گریز
        public bool? EnableNightShiftPreference { get; set; }   //فعال کردن تنظیمات شیفت شب
        public int? NightShiftPreferenceType { get; set; } // 0=شب‌دوست، 1=شب‌گریز، 2=خنثی
        public double? NightShiftPreferenceWeight { get; set; }

        /// <summary>وزن نرم الزام حضور مسئول (تعداد روی تعریف شیفت است)</summary>
        public double? ShiftManagerRequirementWeight { get; set; }

        // تنظیمات توزیع بر اساس سابقه — تفکیک صبح / عصر / شب
        // نوع: 0=اولویت سابقه بیشتر، 1=اولویت سابقه کمتر، 2=خنثی
        public bool? EnableMorningShiftDistributionBySeniority { get; set; }
        public int? MorningShiftDistributionType { get; set; }
        public double? MorningShiftDistributionWeight { get; set; }

        public bool? EnableEveningShiftDistributionBySeniority { get; set; }
        public int? EveningShiftDistributionType { get; set; }
        public double? EveningShiftDistributionWeight { get; set; }

        public bool? EnableNightShiftDistributionBySeniority { get; set; }
        public int? NightShiftDistributionType { get; set; }
        public double? NightShiftDistributionWeight { get; set; }
        public double? SeniorityDistributionSlope { get; set; }

        /// <summary>امکان شیفت‌بندی ماه جاری (توسعه/تست)</summary>
        public bool? AllowCurrentMonthScheduling { get; set; }

        /// <summary>شیفت‌بندی مجدد ماهانه با حذف خودکار برنامه قبلی</summary>
        public bool? AllowMonthlyRescheduleWithAutoDelete { get; set; }

        /// <summary>اجازه شیفت عصر در روز بعد از شب (null = بدون تغییر هنگام ویرایش)</summary>
        public bool? AllowEveningAfterNightShift { get; set; }

        /// <summary>اجازه شیفت شب در روز بعد از شب (null = بدون تغییر هنگام ویرایش)</summary>
        public bool? AllowNightShiftAfterNightShift { get; set; }
    }
    public class DepartmentSchedulingSettingsDtoGet : DepartmentSchedulingSettingsDtoAdd
    {
        public int Id { get; set; }
    }
}
