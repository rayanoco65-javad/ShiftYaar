using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.ShiftModel
{
    /// <summary>
    /// نیازمندی یک مسئولیت در یک شیفت مشخص — با امکان تفکیک جنسیت (مرد/زن) یا شناور (تعداد کل)
    /// و همچنین تفکیک برای روزهای عادی و تعطیل.
    /// </summary>
    public class ShiftRequiredResponsibility : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("Shift")]
        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }

        [ForeignKey("DepartmentResponsibility")]
        public int? DepartmentResponsibilityId { get; set; }
        public DepartmentResponsibility? DepartmentResponsibility { get; set; }

        // --- روزهای عادی (غیرتعطیل) ---
        public int? RequiredMaleCount { get; set; }
        public int? RequiredFemaleCount { get; set; }
        public int? RequiredTotalCount { get; set; }

        // --- روزهای تعطیل (اختیاری؛ اگر خالی باشد همان روز عادی اعمال می‌شود) ---
        public int? HolidayRequiredMaleCount { get; set; }
        public int? HolidayRequiredFemaleCount { get; set; }
        public int? HolidayRequiredTotalCount { get; set; }

        public ShiftRequiredResponsibility()
        {
            this.Id = null;
            this.ShiftId = null;
            this.Shift = null;
            this.DepartmentResponsibilityId = null;
            this.DepartmentResponsibility = null;
            this.RequiredMaleCount = null;
            this.RequiredFemaleCount = null;
            this.RequiredTotalCount = null;
            this.HolidayRequiredMaleCount = null;
            this.HolidayRequiredFemaleCount = null;
            this.HolidayRequiredTotalCount = null;
        }
    }
}
