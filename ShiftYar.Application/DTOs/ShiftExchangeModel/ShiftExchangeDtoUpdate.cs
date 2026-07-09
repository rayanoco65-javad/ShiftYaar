using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftExchangeModel
{
    public class ShiftExchangeDtoUpdate
    {
        [Required(ErrorMessage = "شناسه درخواست الزامی است")]
        public int Id { get; set; }

        [StringLength(500, ErrorMessage = "دلیل درخواست نمی‌تواند بیش از 500 کاراکتر باشد")]
        public string? Reason { get; set; }
    }
}
