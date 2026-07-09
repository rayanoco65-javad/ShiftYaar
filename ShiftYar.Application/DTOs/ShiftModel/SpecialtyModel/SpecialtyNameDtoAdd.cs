using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel
{
    public class SpecialtyNameDtoAdd
    {
        [Required(ErrorMessage = "نام تخصص الزامی است.")]
        public string? Name { get; set; }
    }
}
