using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel
{
    public class ShiftDtoAdd
    {
        [Required(ErrorMessage = "شناسه بخش الزامی است")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "نوع شیفت الزامی است")]
        public ShiftLabel Label { get; set; }

        [Required(ErrorMessage = "زمان شروع الزامی است")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "زمان پایان الزامی است")]
        public TimeSpan EndTime { get; set; }
    }
}
