using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.AddressModel
{
    public class Province
    {
        [Key]
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }

        public Province()
        {
            this.Id = null;
            this.Name = null;
            this.Slug = null;
        }
    }
}
