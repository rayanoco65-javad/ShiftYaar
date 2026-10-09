using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.DepartmentModel.DepartmentResponsibilityModel
{
    public class DepartmentResponsibilityDtoUpdate
    {
        [Required(ErrorMessage = "شناسه مسئولیت الزامی است")]
        public int Id { get; set; }

        public int? SpecialtyId { get; set; }

        [Required(ErrorMessage = "عنوان مسئولیت الزامی است")]
        [MaxLength(100, ErrorMessage = "عنوان مسئولیت حداکثر ۱۰۰ کاراکتر است")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "توضیحات حداکثر ۵۰۰ کاراکتر است")]
        public string? Description { get; set; }

        public bool? IsDefault { get; set; }

        public int? Priority { get; set; }

        public bool? IsActive { get; set; }
    }
}
