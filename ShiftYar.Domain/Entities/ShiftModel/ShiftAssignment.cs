using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.ShiftModel
{
    public class ShiftAssignment : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("ShiftDate")]
        public int? ShiftDateId { get; set; } // تاریخ اجرای این شیفت
        public ShiftDate? ShiftDate { get; set; }

        [ForeignKey("Shift")]
        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User? User { get; set; }

        public bool? IsOnCall { get; set; } // آیا آنکال بوده یا در محل حاضر

        public string? Notes { get; set; } // توضیحات اختیاری

        public ShiftAssignment()
        {
            this.Id = null;
            this.ShiftDate = null;
            this.ShiftId = null;
            this.Shift = null;
            this.UserId = 0;
            this.User = null;
            this.IsOnCall = null;
            this.Notes = null;
        }
    }

}
