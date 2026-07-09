using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.PermissionModel;
using ShiftYar.Domain.Entities.RoleModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.RolePermissionModel
{
    public class RolePermission : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("Role")]
        public int? RoleId { get; set; }
        public Role Role { get; set; }

        [ForeignKey("Permission")]
        public int? PermissionId { get; set; }
        public Permission Permission { get; set; }

        public RolePermission()
        {
            Id = null;
            RoleId = null;
            PermissionId = null;
            Role = null;
            Permission = null;
        }
    }
}
