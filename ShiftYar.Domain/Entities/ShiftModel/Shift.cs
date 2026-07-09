using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Domain.Entities.ShiftModel
{
    public class Shift : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("Department")]
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public ShiftLabel? Label { get; set; } // صبح، عصر، شب

        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        public ICollection<ShiftRequiredSpecialty>? RequiredSpecialties { get; set; }
    }
}
