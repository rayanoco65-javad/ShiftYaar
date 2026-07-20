using System;

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
        /// <summary>درصد صعوبت کار (۰ تا ۱۰۰). کاهش هفتگی از ۸٪ به بالا اعمال می‌شود.</summary>
        public decimal HardshipPercent { get; set; }
        public bool HasUncommonRotatingShifts { get; set; }
    }
}
