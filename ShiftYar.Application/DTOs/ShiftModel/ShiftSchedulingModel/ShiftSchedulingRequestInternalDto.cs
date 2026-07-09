using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel
{
    /// <summary>
    /// درخواست بهینه‌سازی شیفت‌بندی (برای استفاده داخلی با تاریخ میلادی)
    /// </summary>
    public class ShiftSchedulingRequestInternalDto
    {
        public int DepartmentId { get; set; } // شناسه دپارتمان هدف
        public DateTime StartDate { get; set; } // تاریخ شروع بازه (میلادی)
        public DateTime EndDate { get; set; } // تاریخ پایان بازه (میلادی)
        public SchedulingAlgorithm Algorithm { get; set; } = SchedulingAlgorithm.SimulatedAnnealing; // الگوریتم انتخابی
    }

    /// <summary>
    /// محدودیت کاربر (برای استفاده داخلی با تاریخ میلادی)
    /// </summary>
    public class UserConstraintInternalDto
    {
        public int UserId { get; set; } // شناسه کاربر
        public List<DateTime> UnavailableDates { get; set; } = new List<DateTime>(); // تاریخ‌های عدم حضور (میلادی)
        public List<ShiftLabel> PreferredShifts { get; set; } = new List<ShiftLabel>(); // شیفت‌های ترجیحی
        public List<ShiftLabel> UnwantedShifts { get; set; } = new List<ShiftLabel>(); // شیفت‌های ناخواسته
        public int MaxConsecutiveShifts { get; set; } = 3; // سقف شیفت متوالی
        public int MinRestDaysBetweenShifts { get; set; } = 1; // حداقل روز استراحت
        public int MaxShiftsPerWeek { get; set; } = 5; // سقف شیفت هفتگی
        public int MaxNightShiftsPerMonth { get; set; } = 8; // سقف شیفت شب ماهانه
    }
}
