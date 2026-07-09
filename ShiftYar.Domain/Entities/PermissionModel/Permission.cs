using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.RolePermissionModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.PermissionModel
{
    public class Permission : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public string Name { get; set; } // مثلا: ViewUsers, EditHospital, etc.
        public string? Description { get; set; } // توضیحات مربوط به پرمیژن
        public bool? IsActive { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; }

        public Permission()
        {
            Id = null;
            Name = null;
            Description = null;
            IsActive = null;
            RolePermissions = new List<RolePermission>();
        }
    }
}
