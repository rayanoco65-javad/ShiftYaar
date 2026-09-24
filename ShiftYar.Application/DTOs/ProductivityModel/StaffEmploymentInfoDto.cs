using System;
using ShiftYar.Domain.Entities.ProductivityModel;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    /// <summary>
    /// Represents the employment attributes that the productivity calculator needs from the caller.
    /// Supports dual hardship input (Percentage and Civil Service Score) and clinical management recognition.
    /// </summary>
    public class StaffEmploymentInfoDto
    {
        public int StaffId { get; set; }
        public string? StaffFullName { get; set; }
        public DateTime? DateOfEmployment { get; set; }
        public int? YearsOfServiceOverride { get; set; }
        public int? ClinicalExperienceYears { get; set; }
        /// <summary>آیا پرسنل مشمول قانون ارتقای بهره‌وری است (true) یا غیرمشمول/عادی (false).</summary>
        public bool IsIncludedInProductivityPlan { get; set; } = true;
        /// <summary>نام یا کد بخش (مثلاً ICU, CCU, Emergency, Burn, Dialysis, Psych, General).</summary>
        public string? DepartmentSectionType { get; set; }
        /// <summary>آیا بخش از نوع ویژه/پراسترس است.</summary>
        public bool IsSpecialSection { get; set; }
        /// <summary>درصد صعوبت کار (۰ تا ۱۰۰).</summary>
        public decimal HardshipPercent { get; set; }
        /// <summary>نام مستعار درصد سختی کار.</summary>
        public decimal? HardshipPercentage
        {
            get => HardshipPercent;
            set => HardshipPercent = value ?? 0m;
        }
        /// <summary>امتیاز سختی کار قانون مدیریت خدمات کشوری (اختیاری/Nullable - انحصاری متقابل با درصد).</summary>
        public decimal? HardshipScore { get; set; }
        /// <summary>نام مستعار امتیاز سختی کار.</summary>
        public decimal? HardshipPoints
        {
            get => HardshipScore;
            set => HardshipScore = value;
        }
        /// <summary>پست سازمانی پرسنل</summary>
        public string? Position { get; set; }
        /// <summary>عنوان شغلی پرسنل</summary>
        public string? JobTitle { get; set; }
        /// <summary>عنوان نقش پرسنل</summary>
        public string? Role { get; set; }
        /// <summary>آیا سوپروایزر است (ماده ۴ دستورالعمل اجرایی: ۲ ساعت کسر خودکار صعوبت)</summary>
        public bool? IsSupervisor { get; set; }
        /// <summary>آیا سرپرستار است (ماده ۴ دستورالعمل اجرایی: ۲ ساعت کسر خودکار صعوبت)</summary>
        public bool? IsHeadNurse { get; set; }
        public bool HasUncommonRotatingShifts { get; set; }
        /// <summary>الگوی گردش شیفت پرسنل در ماه (ثابت روزکار، دو نوبته، سه نوبته کامل، ثابت شب).</summary>
        public ShiftPatternType? ShiftPattern { get; set; }
    }
}
