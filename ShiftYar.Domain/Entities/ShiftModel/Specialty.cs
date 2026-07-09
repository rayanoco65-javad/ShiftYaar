using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.HospitalModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.ShiftModel
{
    ///این مدل مشخص می‌کند در یک بخش مشخص از یک بیمارستان مشخص، چه تخصص‌هایی برای حضور در برنامه شیفت بندی لازم است.
    public class Specialty : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("Department")]
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public string? SpecialtyName { get; set; } // نام تخصص مثل: هوشبری، اتاق عمل، پرستاری

        public Specialty()
        {
            this.Id = null;
            this.DepartmentId = null;
            this.Department = null;
            this.SpecialtyName = null;
        }
    }
}
