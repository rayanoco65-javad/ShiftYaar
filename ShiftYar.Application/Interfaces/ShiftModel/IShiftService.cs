using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Application.Features.ShiftModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.ShiftModel
{
    public interface IShiftService
    {
        Task<ApiResponse<PagedResponse<ShiftDtoGet>>> GetShifts(ShiftFilter filter);
        Task<ApiResponse<ShiftDtoGet>> GetShift(int id);
        Task<ApiResponse<ShiftDtoGet>> CreateShift(ShiftDtoAdd dto);
        Task<ApiResponse<ShiftDtoGet>> UpdateShift(int id, ShiftDtoAdd dto);
        Task<ApiResponse<string>> DeleteShift(int id);
    }
}
