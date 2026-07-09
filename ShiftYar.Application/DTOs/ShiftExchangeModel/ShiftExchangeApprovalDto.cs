using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftExchangeModel
{
    public class ShiftExchangeApprovalDto
    {
        [Required(ErrorMessage = "شناسه درخواست الزامی است")]
        public int Id { get; set; }

        [Required(ErrorMessage = "وضعیت تأیید الزامی است")]
        public bool IsApproved { get; set; }

        [StringLength(500, ErrorMessage = "نظر سوپروایزر نمی‌تواند بیش از 500 کاراکتر باشد")]
        public string? SupervisorComment { get; set; }
    }
}
