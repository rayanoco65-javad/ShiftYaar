using System;
using ShiftYar.Domain.Entities.ProductivityModel;

namespace ShiftYar.Application.DTOs.ProductivityModel
{
    /// <summary>
    /// Represents the employment attributes that the productivity calculator needs from the caller.
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
        public bool HasUncommonRotatingShifts { get; set; }
        /// <summary>الگوی گردش شیفت پرسنل در ماه (ثابت روزکار، دو نوبته، سه نوبته کامل، ثابت شب).</summary>
        public ShiftPatternType? ShiftPattern { get; set; }
    }
}
