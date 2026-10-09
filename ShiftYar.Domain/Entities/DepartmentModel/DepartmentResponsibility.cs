using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.DepartmentModel
{
    /// <summary>
    /// تعریف مسئولیت‌ها و نقش‌های تخصصی موجود در یک بخش (مثلاً در اتاق عمل: سیرکولر، اسکراب اول، اسکراب دوم، اد، وینیست)
    /// </summary>
    public class DepartmentResponsibility : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("Department")]
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        /// <summary>
        /// تخصص مربوطه (اختیاری؛ برای فیلتر کردن هوشمند در فرانت‌اند؛ مثلاً اسکراب فقط برای تخصص اتاق عمل است)
        /// </summary>
        [ForeignKey("Specialty")]
        public int? SpecialtyId { get; set; }
        public Specialty? Specialty { get; set; }

        /// <summary>
        /// عنوان مسئولیت (مثلاً: سیرکولر، اسکراب اول، وینیست، ...)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string? Title { get; set; }

        /// <summary>
        /// توضیحات اختیاری
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// آیا مسئولیت پیش‌فرض/پایه است که همه پرسنل به طور خودکار قادر به انجام آن هستند؟ (مثل سیرکولر)
        /// </summary>
        public bool? IsDefault { get; set; }

        /// <summary>
        /// اولویت تخصیص در الگوریتم زمان‌بندی (نقش‌های با اولویت بالاتر مثل وینیست و اسکراب اول زودتر تخصیص می‌یابند)
        /// </summary>
        public int? Priority { get; set; }

        public bool? IsActive { get; set; }

        public ICollection<UserDepartmentResponsibility>? UserResponsibilities { get; set; }
        public ICollection<ShiftRequiredResponsibility>? ShiftRequiredResponsibilities { get; set; }

        public DepartmentResponsibility()
        {
            this.Id = null;
            this.DepartmentId = null;
            this.Department = null;
            this.SpecialtyId = null;
            this.Specialty = null;
            this.Title = null;
            this.Description = null;
            this.IsDefault = false;
            this.Priority = 0;
            this.IsActive = true;
            this.UserResponsibilities = new List<UserDepartmentResponsibility>();
            this.ShiftRequiredResponsibilities = new List<ShiftRequiredResponsibility>();
        }
    }
}
