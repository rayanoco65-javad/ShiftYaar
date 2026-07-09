using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel
{
    public class ShiftScheduleDtoGet
    {
        public int Id { get; set; }

        // ---- Shift Date ----
        public int? ShiftDateId { get; set; }
        public DateTime? Date { get; set; }
        public string? PersianDate { get; set; }
        public string? DayTitle { get; set; }
        public bool? IsHoliday { get; set; }
        public string? HolidayEvent { get; set; }

        // ---- Shift Info ----
        public int? ShiftId { get; set; }
        public string? ShiftTitle { get; set; }          // مثلاً صبح، عصر، شب
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        // ---- User Info ----
        public int? UserId { get; set; }
        public string? UserFullName { get; set; }
        public string? PersonnelCode { get; set; }
        public string? NationalCode { get; set; }
        public string? UserPhoneNumber { get; set; }
        public bool? IsProjectPersonnel { get; set; }
        public bool? IsActive { get; set; }
        public UserGender? Gender { get; set; }

        // ---- Department Info ----
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public bool? DepartmentIsNightLover { get; set; }

        // ---- Specialty Info ----
        public int? SpecialtyId { get; set; }
        public string? SpecialtyName { get; set; }

        // ---- Assignment Info ----
        public bool? IsOnCall { get; set; }
        public string? Notes { get; set; }
    }
}
