using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.RolePermissionModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.RoleModel
{
    public class Role : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public string? Name { get; set; }
        public bool? IsActive { get; set; }

        public ICollection<UserRole>? UserRoles { get; set; }
        public ICollection<RolePermission>? RolePermissions { get; set; }

        public Role()
        {
            this.Id = null;
            this.Name = null;
            this.IsActive = null;
            this.UserRoles = new List<UserRole>();
            this.RolePermissions = new List<RolePermission>();
        }
    }
}
