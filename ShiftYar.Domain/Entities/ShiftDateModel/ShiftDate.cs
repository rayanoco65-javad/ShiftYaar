using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.ShiftDateModel
{
    public class ShiftDate : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public string? DayTitle { get; set; }  //مثلا شنبه، یکشنبه، دوشنبه و ...
        public DateTime? Date { get; set; }
        public string? PersianDate { get; set; } = default!;
        public bool? IsHoliday { get; set; }
        public string? HolidayEvent { get; set; }   //مناسبت روز تعطیل

        public ICollection<ShiftAssignment>? ShiftAssignments { get; set; }

        public ShiftDate()
        {
            this.Id = null;
            this.DayTitle = null;
            this.Date = null;
            this.PersianDate = null;
            this.IsHoliday = null;
            this.HolidayEvent = null;
            this.ShiftAssignments = null;
        }
    }
}
