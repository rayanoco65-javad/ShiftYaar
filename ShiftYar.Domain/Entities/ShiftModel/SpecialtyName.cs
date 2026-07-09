using ShiftYar.Domain.Entities.BaseModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.ShiftModel
{
    public class SpecialtyName : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public string? Name { get; set; }

        public SpecialtyName()
        {
            this.Id = null;
            this.Name = null;
        }
    }
}
