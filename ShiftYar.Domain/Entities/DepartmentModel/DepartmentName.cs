using ShiftYar.Domain.Entities.BaseModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.DepartmentModel
{
    public class DepartmentName : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public string? Name { get; set; }

        public DepartmentName()
        {
            this.Id = null;
            this.Name = null;
        }
    }
}
