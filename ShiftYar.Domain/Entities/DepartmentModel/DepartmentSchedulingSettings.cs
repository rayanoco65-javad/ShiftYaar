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
        public bool? EnforceMaxConsecutiveShifts { get; set; } // اعمال حداکثر شیفت‌های متوالی
        public bool? EnforceWeeklyMaxShifts { get; set; } // اعمال سقف هفتگی شیفت‌ها
        public bool? EnforceNightShiftMonthlyCap { get; set; } // اعمال سقف شیفت شب ماهانه
        public bool? EnforceSpecialtyCapacity { get; set; } // جلوگیری از تجاوز از ظرفیت تخصص/شیفت/روز

        // مقادیر عددی قوانین (برای ساده‌سازی ورود اطلاعات توسط سوپروایزر)
        public int? MinRestDaysBetweenShifts { get; set; } // حداقل روزهای استراحت بین شیفت‌ها
        public int? MaxConsecutiveShifts { get; set; } // حداکثر شیفت‌های متوالی
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

        // تنظیمات الزام مسئول شیفت
        public bool? RequireManagerForEveningShift { get; set; } // الزام حضور مسئول در شیفت عصر
        public bool? RequireManagerForNightShift { get; set; } // الزام حضور مسئول در شیفت شب
        public double? ShiftManagerRequirementWeight { get; set; } // وزن الزام حضور مسئول در شیفت‌ها

        // تنظیمات توزیع شیفت‌های شب باقی‌مانده بر اساس سابقه
        public bool? EnableNightShiftDistributionBySeniority { get; set; } // فعال‌سازی توزیع شیفت‌های شب بر اساس سابقه
        public int? NightShiftDistributionType { get; set; } // نوع توزیع: 0=شب‌دوست (سابقه بیشتر اولویت), 1=شب‌گریز (سابقه کمتر اولویت), 2=خنثی
        public double? NightShiftDistributionWeight { get; set; } // وزن توزیع شیفت‌های شب بر اساس سابقه
        public double? SeniorityDistributionSlope { get; set; } // شیب توزیع بر اساس سابقه (مقدار پیش‌فرض: 1.0)
    }
}
