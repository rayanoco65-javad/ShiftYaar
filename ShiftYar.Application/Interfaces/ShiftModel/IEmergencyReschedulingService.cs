using System.Threading.Tasks;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;

namespace ShiftYar.Application.Interfaces.ShiftModel
{
    public interface IEmergencyReschedulingService
    {
        Task<ApiResponse<RollingHorizonRescheduleResultDto>> RescheduleAsync(EmergencyReschedulingRequestDto request);
    }
}


