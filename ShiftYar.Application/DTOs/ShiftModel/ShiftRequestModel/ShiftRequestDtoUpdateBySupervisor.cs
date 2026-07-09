using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel
{
    public class ShiftRequestDtoUpdateBySupervisor
    {
        //public int Id { get; set; }
        public RequestStatus Status { get; set; }
        public string? SupervisorComment { get; set; }
    }
}
