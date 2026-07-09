using ShiftYar.Domain.Entities.BaseModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.ShiftModel
{
    ///این مدل مشخص می‌کند در یک شیفت خاص، چه تخصص‌هایی با چه تعداد نیرو لازم هستند
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

        public int? RequiredMaleCount { get; set; } //حداقل تعداد نیروهای مرد
        public int? RequiredFemaleCount { get; set; } //حداقل تعداد نیروهای زن
        public int? RequiredTottalCount { get; set; }  //تعداد کل نیروهای موردنیاز در شیفت صرفنظر از جنسیت

        public int? OnCallMaleCount { get; set; } //حداقل تعداد نیروهای مرد آنکال
        public int? OnCallFemaleCount { get; set; } //حداقل تعداد نیروهای زن آنکال
        public int? OnCallTottalCount { get; set; }  //تعداد کل نیروهای آنکال شیفت صرفنظر از جنسیت

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
        }
    }
}
