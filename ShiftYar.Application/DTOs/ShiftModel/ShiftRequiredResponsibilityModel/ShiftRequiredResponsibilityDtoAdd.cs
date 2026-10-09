using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredResponsibilityModel
{
    public class ShiftRequiredResponsibilityDtoAdd
    {
        [Required(ErrorMessage = "شناسه شیفت الزامی است")]
        public int ShiftId { get; set; }

        [Required(ErrorMessage = "شناسه مسئولیت الزامی است")]
        public int DepartmentResponsibilityId { get; set; }

        // روزهای غیرتعطیل
        public int? RequiredMaleCount { get; set; }
        public int? RequiredFemaleCount { get; set; }

        [Required(ErrorMessage = "تعداد کل مورد نیاز الزامی است")]
        [Range(1, int.MaxValue, ErrorMessage = "تعداد مورد نیاز باید بزرگتر از صفر باشد")]
        public int? RequiredTotalCount { get; set; }

        // روزهای تعطیل (اختیاری؛ در صورت خالی بودن همان غیرتعطیل اعمال می‌شود)
        public int? HolidayRequiredMaleCount { get; set; }
        public int? HolidayRequiredFemaleCount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "تعداد مورد نیاز تعطیل نمی‌تواند منفی باشد")]
        public int? HolidayRequiredTotalCount { get; set; }
    }
}
