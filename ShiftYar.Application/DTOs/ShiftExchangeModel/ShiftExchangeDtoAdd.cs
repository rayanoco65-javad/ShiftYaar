using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftExchangeModel
{
    public class ShiftExchangeDtoAdd
    {
        [Required(ErrorMessage = "شناسه کاربر درخواست کننده الزامی است")]
        public int RequestingUserId { get; set; }

        [Required(ErrorMessage = "شناسه کاربر پیشنهاد دهنده الزامی است")]
        public int OfferingUserId { get; set; }

        [Required(ErrorMessage = "شناسه شیفت درخواست کننده الزامی است")]
        public int RequestingShiftAssignmentId { get; set; }

        [Required(ErrorMessage = "شناسه شیفت پیشنهاد دهنده الزامی است")]
        public int OfferingShiftAssignmentId { get; set; }

        [Required(ErrorMessage = "دلیل درخواست الزامی است")]
        [StringLength(500, ErrorMessage = "دلیل درخواست نمی‌تواند بیش از 500 کاراکتر باشد")]
        public string Reason { get; set; } = string.Empty;
    }
}
