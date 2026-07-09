using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel
{
    public class ShiftRequestDtoGet
    {
        public int? Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public DateTime? RequestDate { get; set; }  //تاریخ مورد درخواست
        //public int? ShiftDateId { get; set; }
        //public ShiftDate? ShiftDate { get; set; }
        public RequestType? RequestType { get; set; }
        public ShiftLabel? ShiftLabel { get; set; }
        public RequestAction? RequestAction { get; set; }
        public RequestStatus? Status { get; set; }
        public string? Reason { get; set; }
        public int? SupervisorId { get; set; }
        public string? SupervisorFullName { get; set; }
        public string? SupervisorComment { get; set; }
        public DateTime? ApprovalDate { get; set; }
    }
}
