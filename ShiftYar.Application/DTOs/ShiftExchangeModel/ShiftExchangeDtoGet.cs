using ShiftYar.Domain.Enums.ShiftExchangeModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftExchangeModel
{
    public class ShiftExchangeDtoGet
    {
        public int? Id { get; set; }
        public int? RequestingUserId { get; set; }
        public string? RequestingUserFullName { get; set; }
        public int? OfferingUserId { get; set; }
        public string? OfferingUserFullName { get; set; }
        public int? RequestingShiftAssignmentId { get; set; }
        public int? OfferingShiftAssignmentId { get; set; }
        public ExchangeStatus? Status { get; set; }
        public DateTime? RequestDate { get; set; }
        public string? Reason { get; set; }
        public int? SupervisorId { get; set; }
        public string? SupervisorFullName { get; set; }
        public string? SupervisorComment { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? ExecutionDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
