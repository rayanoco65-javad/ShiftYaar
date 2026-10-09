using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.DepartmentModel.DepartmentResponsibilityModel
{
    public class DepartmentResponsibilityDtoAdd
    {
        [Required(ErrorMessage = "شناسه بخش الزامی است")]
        public int DepartmentId { get; set; }

        public int? SpecialtyId { get; set; }

        [Required(ErrorMessage = "عنوان مسئولیت الزامی است")]
        [MaxLength(100, ErrorMessage = "عنوان مسئولیت حداکثر ۱۰۰ کاراکتر است")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "توضیحات حداکثر ۵۰۰ کاراکتر است")]
        public string? Description { get; set; }

        /// <summary>
        /// آیا این مسئولیت پیش‌فرض است؟ (مانند سیرکولر که همه پرسنل می‌توانند انجام دهند)
        /// </summary>
        public bool? IsDefault { get; set; }

        /// <summary>
        /// اولویت تخصیص در شیفت‌بندی (اولویت بالاتر = تخصیص زودهنگام در الگوریتم)
        /// </summary>
        public int? Priority { get; set; }
    }
}
