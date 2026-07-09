using ShiftYar.Domain.Entities.BaseModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.UserModel
{
    public class LoginHistory : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public DateTime? LoginTime { get; set; }
        public string? IPAddress { get; set; }
        public string? Device { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User? User { get; set; }

        public LoginHistory()
        {
            this.Id = null;
            this.LoginTime = null;
            this.IPAddress = null;
            this.Device = null;
            this.UserId = null;
            this.User = null;
        }
    }
}
