using ShiftYar.Domain.Entities.HospitalModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShiftYar.Domain.Entities.BaseModel;

namespace ShiftYar.Domain.Entities.UserModel
{
    public class UserPhoneNumber : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User? User { get; set; }

        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }

        public UserPhoneNumber()
        {
            this.Id = null;
            this.UserId = null;
            this.User = null;
            this.PhoneNumber = null;
            this.IsActive = null;
        }
    }
}
