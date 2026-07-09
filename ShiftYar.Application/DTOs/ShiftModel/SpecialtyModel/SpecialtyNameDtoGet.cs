using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel
{
    public class SpecialtyNameDtoGet
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime? CreateDate { get; set; }
        public DateTime? UpdateDate { get; set; }
        public int? TheUserId { get; set; }
    }
}
