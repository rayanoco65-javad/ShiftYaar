using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredResponsibilityModel;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.ShiftRequiredResponsibilityModel
{
    public interface IShiftRequiredResponsibilityService
    {
        Task<ApiResponse<List<ShiftRequiredResponsibilityDtoGet>>> GetByShiftIdAsync(int shiftId);
        Task<ApiResponse<ShiftRequiredResponsibilityDtoGet>> GetByIdAsync(int id);
        Task<ApiResponse<ShiftRequiredResponsibilityDtoGet>> CreateAsync(ShiftRequiredResponsibilityDtoAdd dto);
        Task<ApiResponse<ShiftRequiredResponsibilityDtoGet>> UpdateAsync(int id, ShiftRequiredResponsibilityDtoAdd dto);
        Task<ApiResponse<string>> DeleteAsync(int id);
    }
}
