using ShiftYar.Domain.Entities.BaseModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.ShiftModel
{
    /// <summary>
    /// نیازمندی تخصص در یک شیفت — با امکان تعریف جداگانه برای روزهای غیرتعطیل و تعطیل.
    /// اگر فیلدهای Holiday* خالی باشند، همان مقادیر روز غیرتعطیل اعمال می‌شود.
    /// </summary>
    public class ShiftRequiredSpecialty : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("Shift")]
        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }

        [ForeignKey("Specialty")]
        public int? SpecialtyId { get; set; }
        public Specialty? Specialty { get; set; }

        // --- روزهای غیرتعطیل (پیش‌فرض) ---
        public int? RequiredMaleCount { get; set; }
        public int? RequiredFemaleCount { get; set; }
        public int? RequiredTottalCount { get; set; }

        public int? OnCallMaleCount { get; set; }
        public int? OnCallFemaleCount { get; set; }
        public int? OnCallTottalCount { get; set; }

        // --- روزهای تعطیل (اختیاری؛ null = همان غیرتعطیل) ---
        public int? HolidayRequiredMaleCount { get; set; }
        public int? HolidayRequiredFemaleCount { get; set; }
        public int? HolidayRequiredTottalCount { get; set; }

        public int? HolidayOnCallMaleCount { get; set; }
        public int? HolidayOnCallFemaleCount { get; set; }
        public int? HolidayOnCallTottalCount { get; set; }

        public ShiftRequiredSpecialty()
        {
            this.Id = null;
            this.ShiftId = null;
            this.Shift = null;
            this.SpecialtyId = null;
            this.Specialty = null;
            this.RequiredMaleCount = null;
            this.RequiredFemaleCount = null;
            this.RequiredTottalCount = null;
            this.OnCallMaleCount = null;
            this.OnCallFemaleCount = null;
            this.OnCallTottalCount = null;
            this.HolidayRequiredMaleCount = null;
            this.HolidayRequiredFemaleCount = null;
            this.HolidayRequiredTottalCount = null;
            this.HolidayOnCallMaleCount = null;
            this.HolidayOnCallFemaleCount = null;
            this.HolidayOnCallTottalCount = null;
        }
    }
}
