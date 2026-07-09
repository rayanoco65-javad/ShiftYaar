using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShiftYar.Domain.Enums.ShiftExchangeModel;

namespace ShiftYar.Domain.Entities.ShiftExchangeModel
{
    public class ShiftExchange : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        // کاربر درخواست کننده
        [ForeignKey("RequestingUser")]
        public int? RequestingUserId { get; set; }
        public User? RequestingUser { get; set; }

        // کاربر پیشنهاد دهنده
        [ForeignKey("OfferingUser")]
        public int? OfferingUserId { get; set; }
        public User? OfferingUser { get; set; }

        // شیفت درخواست کننده
        [ForeignKey("RequestingShiftAssignment")]
        public int? RequestingShiftAssignmentId { get; set; }
        public ShiftAssignment? RequestingShiftAssignment { get; set; }

        // شیفت پیشنهاد دهنده
        [ForeignKey("OfferingShiftAssignment")]
        public int? OfferingShiftAssignmentId { get; set; }
        public ShiftAssignment? OfferingShiftAssignment { get; set; }

        // وضعیت درخواست
        public ExchangeStatus? Status { get; set; }

        // تاریخ ایجاد درخواست
        public DateTime? RequestDate { get; set; }

        // دلیل درخواست
        public string? Reason { get; set; }

        // اطلاعات تأیید سوپروایزر
        [ForeignKey("Supervisor")]
        public int? SupervisorId { get; set; }
        public User? Supervisor { get; set; }
        public string? SupervisorComment { get; set; }
        public DateTime? ApprovalDate { get; set; }

        // تاریخ اجرای جابجایی
        public DateTime? ExecutionDate { get; set; }

        public ShiftExchange()
        {
            this.Id = null;
            this.RequestingUserId = null;
            this.RequestingUser = null;
            this.OfferingUserId = null;
            this.OfferingUser = null;
            this.RequestingShiftAssignmentId = null;
            this.RequestingShiftAssignment = null;
            this.OfferingShiftAssignmentId = null;
            this.OfferingShiftAssignment = null;
            this.Status = null;
            this.RequestDate = null;
            this.Reason = null;
            this.SupervisorId = null;
            this.Supervisor = null;
            this.SupervisorComment = null;
            this.ApprovalDate = null;
            this.ExecutionDate = null;
        }
    }
}
