using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.DepartmentModel
{
    public class DepartmentNameDtoAdd
    {
        [Required(ErrorMessage = "نام دپارتمان الزامی است.")]
        public string? Name { get; set; }
    }
}
