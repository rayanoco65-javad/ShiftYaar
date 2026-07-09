using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel
{
    /// <summary>
    /// محدودیت‌های شیفت‌بندی
    /// </summary>
    public class ShiftSchedulingConstraintDto
    {
        public int UserId { get; set; }

        //public List<DateTime> UnavailableDates { get; set; } = new List<DateTime>();
        public List<string> UnavailableDates { get; set; } = new List<string>(); // تاریخ‌های عدم حضور (شمسی)

        public List<ShiftLabel> PreferredShifts { get; set; } = new List<ShiftLabel>();
        public List<ShiftLabel> UnwantedShifts { get; set; } = new List<ShiftLabel>();
        public int MaxConsecutiveShifts { get; set; } = 3;
        public int MinRestDaysBetweenShifts { get; set; } = 1;
        public int MaxShiftsPerWeek { get; set; } = 5;
        public int MaxNightShiftsPerMonth { get; set; } = 8;
    }
}
