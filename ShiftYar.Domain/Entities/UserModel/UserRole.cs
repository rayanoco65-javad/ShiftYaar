using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.RoleModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.UserModel
{
    public class UserRole : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User? User { get; set; }

        [ForeignKey("Role")]
        public int? RoleId { get; set; }
        public Role? Role { get; set; }

        public UserRole()
        {
            this.Id = null;
            this.UserId = null;
            this.RoleId = null;
            this.User = null;
            this.Role = null;
        }
    }
}
