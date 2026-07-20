using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredSpecialtyModel
{
    public class ShiftRequiredSpecialtyDtoAdd
    {
        [Required(ErrorMessage = "شناسه شیفت الزامی است")]
        public int ShiftId { get; set; }

        [Required(ErrorMessage = "شناسه تخصص الزامی است")]
        public int SpecialtyId { get; set; }

        // روزهای غیرتعطیل
        public int? RequiredMaleCount { get; set; }
        public int? RequiredFemaleCount { get; set; }

        [Required(ErrorMessage = "تعداد مورد نیاز (روز غیرتعطیل) الزامی است")]
        [Range(1, int.MaxValue, ErrorMessage = "تعداد مورد نیاز باید بزرگتر از صفر باشد")]
        public int? RequiredTottalCount { get; set; }

        public int? OnCallMaleCount { get; set; }
        public int? OnCallFemaleCount { get; set; }
        public int? OnCallTottalCount { get; set; }

        // روزهای تعطیل (اختیاری؛ در صورت خالی بودن همان غیرتعطیل اعمال می‌شود)
        public int? HolidayRequiredMaleCount { get; set; }
        public int? HolidayRequiredFemaleCount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "تعداد مورد نیاز تعطیل نمی‌تواند منفی باشد")]
        public int? HolidayRequiredTottalCount { get; set; }

        public int? HolidayOnCallMaleCount { get; set; }
        public int? HolidayOnCallFemaleCount { get; set; }
        public int? HolidayOnCallTottalCount { get; set; }
    }
}
