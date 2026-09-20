using ShiftYar.Domain.Entities.BaseModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.DepartmentModel
{
    public class DepartmentSchedulingSettings : BaseEntity
    {
        [Key]
        public int? Id { get; set; } // شناسه تنظیمات زمان‌بندی دپارتمان

        [ForeignKey("Department")]
        public int? DepartmentId { get; set; } // شناسه دپارتمان
        public Department? Department { get; set; } // دپارتمان مربوطه

        //قوانین سخت و غیرقابل نقض
        public bool? ForbidDuplicateDailyAssignments { get; set; } // ممنوعیت بیش از یک شیفت در روز برای کاربر
        public bool? EnforceMaxShiftsPerDay { get; set; } // اعمال حداکثر شیفت روزانه مطابق تنظیمات سراسری
        public bool? EnforceMinRestDays { get; set; } // اعمال حداقل روزهای استراحت بین شیفت‌ها
        public bool? EnforceMaxConsecutiveShifts { get; set; } // اعمال سقف روزهای کاری متوالی
        public bool? EnforceWeeklyMaxShifts { get; set; } // اعمال سقف هفتگی شیفت‌ها
        public bool? EnforceNightShiftMonthlyCap { get; set; } // اعمال سقف شیفت شب ماهانه
        public bool? EnforceSpecialtyCapacity { get; set; } // جلوگیری از تجاوز از ظرفیت تخصص/شیفت/روز

        // مقادیر عددی قوانین (برای ساده‌سازی ورود اطلاعات توسط سوپروایزر)
        public int? MinRestDaysBetweenShifts { get; set; } // حداقل روزهای استراحت بین شیفت‌ها
        /// <summary>حداکثر روز کاری متوالی؛ اگر خالی باشد در شیفت‌بندی مقدار ۳ استفاده می‌شود.</summary>
        public int? MaxConsecutiveShifts { get; set; }
        public int? MaxShiftsPerWeek { get; set; } // سقف تعداد شیفت در هفته
        public int? MaxNightShiftsPerMonth { get; set; } // سقف شیفت شب ماهانه
        public int? MaxShiftsPerDay { get; set; } // حداکثر شیفت روزانه هر نفر (سراسری)
        public int? MaxConsecutiveNightShifts { get; set; } // حداکثر شیفت شب متوالی (سراسری)

        //قوانین نرم دارای وزن
        public double? GenderBalanceWeight { get; set; } // وزن تعادل جنسیتی
        public double? SpecialtyPreferenceWeight { get; set; } // وزن ترجیح مطابقت تخصص
        public double? UserUnwantedShiftWeight { get; set; } // وزن جریمه شیفت‌های ناخواسته کاربر
        public double? UserPreferredShiftWeight { get; set; } // وزن پاداش شیفت‌های ترجیحی کاربر
        public double? WeeklyMaxWeight { get; set; } // وزن سقف هفتگی شیفت‌ها (نرم)
        public double? MonthlyNightCapWeight { get; set; } // وزن سقف شیفت شب ماهانه (نرم)

        // عدالت و چرخش
        public double? FairShiftCountBalanceWeight { get; set; } // وزن تعادل تعداد شیفت بین افراد
        public double? ExtraShiftRotationWeight { get; set; } // وزن چرخش شیفت‌های اضافه
        public double? ShiftLabelBalanceWeight { get; set; } // وزن تعادل Morning/Evening/Night
        public int? FairnessLookbackMonths { get; set; } //  بازهٔ سابقه برای محاسبه عدالت براساس ماه

        // تنظیمات حداقل شیفت برای انواع مختلف شیفت‌گردشی
        public bool? EnforceMinimumShiftsForRotatingStaff { get; set; } // اعمال حداقل شیفت برای پرسنل گردشی
        public int? MinMorningShiftsForThreeShiftRotation { get; set; } // حداقل شیفت صبح برای گردشی سه نوبت
        public int? MinEveningShiftsForThreeShiftRotation { get; set; } // حداقل شیفت عصر برای گردشی سه نوبت
        public int? MinNightShiftsForThreeShiftRotation { get; set; } // حداقل شیفت شب برای گردشی سه نوبت
        public int? MinFirstShiftForTwoShiftRotation { get; set; } // حداقل شیفت اول برای گردشی دو نوبت
        public int? MinSecondShiftForTwoShiftRotation { get; set; } // حداقل شیفت دوم برای گردشی دو نوبت

        // تنظیمات شب‌دوست/شب‌گریز
        public bool? EnableNightShiftPreference { get; set; } // فعال‌سازی تنظیمات شب‌دوست/شب‌گریز
        public int? NightShiftPreferenceType { get; set; } // نوع تنظیمات شب: 0=شب‌دوست، 1=شب‌گریز، 2=خنثی
        public double? NightShiftPreferenceWeight { get; set; } // وزن تنظیمات شب‌دوست/شب‌گریز

        /// <summary>وزن نرم الزام حضور مسئول شیفت (تعداد مسئول روی تعریف شیفت است).</summary>
        public double? ShiftManagerRequirementWeight { get; set; }

        // تنظیمات توزیع شیفت‌های باقی‌مانده بر اساس سابقه (تفکیک صبح / عصر / شب)
        public bool? EnableMorningShiftDistributionBySeniority { get; set; }
        public int? MorningShiftDistributionType { get; set; } // 0=سابقه بیشتر، 1=سابقه کمتر، 2=خنثی
        public double? MorningShiftDistributionWeight { get; set; }

        public bool? EnableEveningShiftDistributionBySeniority { get; set; }
        public int? EveningShiftDistributionType { get; set; } // 0=سابقه بیشتر، 1=سابقه کمتر، 2=خنثی
        public double? EveningShiftDistributionWeight { get; set; }

        public bool? EnableNightShiftDistributionBySeniority { get; set; }
        public int? NightShiftDistributionType { get; set; } // 0=سابقه بیشتر، 1=سابقه کمتر، 2=خنثی
        public double? NightShiftDistributionWeight { get; set; }
        public double? SeniorityDistributionSlope { get; set; } // شیب مشترک برای هر سه نوع (پیش‌فرض: 1.0)

        // تنظیمات تمایل به اضافه کار و توزیع بر اساس سابقه
        public bool? EnableOvertimeDistributionBySeniority { get; set; } // فعال‌سازی توزیع اضافه کار بر اساس سابقه
        public int? OvertimePreferenceType { get; set; } // نوع تنظیمات اضافه کار: 0=علاقه‌مند، 1=گریزان، 2=خنثی
        public double? OvertimeDistributionWeight { get; set; } // وزن توزیع اضافه کار بر اساس سابقه

        /// <summary>
        /// امکان شیفت‌بندی ماه جاری — فقط برای توسعه/تست.
        /// اگر true باشد، برای ماه شمسی جاری (حتی پس از شروع ماه) Optimize و حذف ماهانه مجاز است.
        /// </summary>
        public bool? AllowCurrentMonthScheduling { get; set; }

        /// <summary>
        /// امکان شیفت‌بندی مجدد ماهانه با حذف خودکار برنامه قبلی.
        /// اگر true باشد، قبل از ذخیرهٔ برنامهٔ جدید، انتساب‌های همان ماه دپارتمان حذف و جایگزین می‌شوند.
        /// </summary>
        public bool? AllowMonthlyRescheduleWithAutoDelete { get; set; }

        /// <summary>
        /// اجازهٔ شیفت عصر در روز بعد از شیفت شب.
        /// پیش‌فرض false: عصر روز بعد ممنوع.
        /// </summary>
        public bool AllowEveningAfterNightShift { get; set; }

        /// <summary>
        /// اجازهٔ شیفت شب در روز بعد از شیفت شب (شب متوالی).
        /// پیش‌فرض false: شب متوالی ممنوع.
        /// </summary>
        public bool AllowNightShiftAfterNightShift { get; set; }
    }
}
