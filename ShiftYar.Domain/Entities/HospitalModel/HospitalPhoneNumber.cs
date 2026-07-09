using ShiftYar.Domain.Entities.BaseModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.HospitalModel
{
    public class HospitalPhoneNumber : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("Hospital")]
        public int? HospitalId { get; set; }
        public Hospital? Hospital { get; set; }

        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }

        public HospitalPhoneNumber()
        {
            this.Id = null;
            this.HospitalId = null;
            this.Hospital = null;
            this.PhoneNumber = null;
            this.IsActive = null;
        }
    }
}
