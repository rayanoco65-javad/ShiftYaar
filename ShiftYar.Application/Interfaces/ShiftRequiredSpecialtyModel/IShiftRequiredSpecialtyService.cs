using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredSpecialtyModel;
using ShiftYar.Application.Features.ShiftRequiredSpecialtyModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.ShiftRequiredSpecialtyModel
{
    public interface IShiftRequiredSpecialtyService
    {
        Task<ApiResponse<PagedResponse<ShiftRequiredSpecialtyDtoGet>>> GetShiftRequiredSpecialties(ShiftRequiredSpecialtyFilter filter);
        Task<ApiResponse<ShiftRequiredSpecialtyDtoGet>> GetShiftRequiredSpecialty(int id);
        Task<ApiResponse<ShiftRequiredSpecialtyDtoGet>> CreateShiftRequiredSpecialty(ShiftRequiredSpecialtyDtoAdd dto);
        Task<ApiResponse<ShiftRequiredSpecialtyDtoGet>> UpdateShiftRequiredSpecialty(int id, ShiftRequiredSpecialtyDtoAdd dto);
        Task<ApiResponse<string>> DeleteShiftRequiredSpecialty(int id);
    }
}
