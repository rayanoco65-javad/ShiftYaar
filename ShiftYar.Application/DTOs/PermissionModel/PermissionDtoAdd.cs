using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.PermissionModel
{
    public class PermissionDtoAdd
    {
        [Required(ErrorMessage = "نام مجوز دسترسی الزامی است.")]
        public string? Name { get; set; }

        public string? Description { get; set; }

        public bool? IsActive { get; set; }
    }
}
