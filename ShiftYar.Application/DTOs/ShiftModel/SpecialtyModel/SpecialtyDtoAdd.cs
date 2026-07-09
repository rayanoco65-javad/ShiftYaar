using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel
{
    public class SpecialtyDtoAdd
    {
        [Required(ErrorMessage = "تعیین دپارتمان الزامی است.")]
        public int? DepartmentId { get; set; }
        public string? SpecialtyName { get; set; } // نام تخصص مثل: هوشبری، اتاق عمل، پرستاری
    }
}
