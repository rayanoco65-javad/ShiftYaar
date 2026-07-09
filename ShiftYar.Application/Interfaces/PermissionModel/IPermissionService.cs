using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.PermissionModel;
using ShiftYar.Application.Features.PermissionModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.PermissionModel
{
    public interface IPermissionService
    {
        Task<ApiResponse<PagedResponse<PermissionDtoGet>>> GetFilteredPermissionsAsync(PermissionFilter filter);
        Task<ApiResponse<PermissionDtoGet>> GetByIdAsync(int id);
        Task<ApiResponse<PermissionDtoGet>> CreateAsync(PermissionDtoAdd dto);
        Task<ApiResponse<PermissionDtoGet>> UpdateAsync(int id, PermissionDtoAdd dto);
        Task<ApiResponse<string>> DeleteAsync(int id);
    }
}
