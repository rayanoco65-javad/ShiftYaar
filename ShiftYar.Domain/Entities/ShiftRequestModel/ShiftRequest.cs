using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using ShiftYar.Domain.Enums.ShiftRequestModel;

namespace ShiftYar.Domain.Entities.ShiftRequestModel
{
    public class ShiftRequest : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User? User { get; set; }

        public DateTime? RequestDate { get; set; }  //تاریخ مورد درخواست
        public RequestType? RequestType { get; set; } //FullDay, SpecificShift
        public ShiftLabel? ShiftLabel { get; set; } //Morning, Evening, Night
        public RequestAction? RequestAction { get; set; } //RequestToBeOnShift, RequestToBeOffShift
        public RequestStatus? Status { get; set; } //Pending, Approved, Rejected
        public string? Reason { get; set; }

        // اطلاعات تأیید
        [ForeignKey("Supervisor")]
        public int? SupervisorId { get; set; }
        public User? Supervisor { get; set; }
        public string? SupervisorComment { get; set; }
        public DateTime? ApprovalDate { get; set; }


        public ShiftRequest()
        {
            this.Id = null;
            this.UserId = null;
            this.User = null;
            this.RequestDate = null;
            //this.ShiftDateId = null;
            //this.ShiftDate = null;
            this.RequestType = null;
            this.ShiftLabel = null;
            this.RequestAction = null;
            this.Status = null;
            this.Reason = null;
            this.SupervisorId = null;
            this.Supervisor = null;
            this.SupervisorComment = null;
            this.ApprovalDate = null;
        }
    }
}
