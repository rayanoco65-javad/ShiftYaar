using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.AddressModel
{
    public class City
    {
        [Key]
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public int? ProvinceId { get; set; }
        public Province? Province { get; set; }

        public City()
        {
            this.Id = null;
            this.Name = null;
            this.Slug = null;
            this.ProvinceId = null;
            this.Province = null;
        }
    }
}
