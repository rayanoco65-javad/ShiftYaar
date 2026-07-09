using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Application.DTOs.RolePermissionModel;
using ShiftYar.Application.Features.RoleModel.Filters;
using ShiftYar.Application.Features.RolePermissionModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.RolePermissionModel
{
    public interface IRolePermissionService
    {
        Task<ApiResponse<PagedResponse<RolePermissionDtoGet>>> GetRolePermissionsAsync(RolePermissionFilter filter);
        Task<ApiResponse<RolePermissionDtoGet>> GetRolePermissionByIdAsync(int id);
        Task<ApiResponse<RolePermissionDtoGet>> CreateRolePermissionAsync(RolePermissionDtoAdd dto);
        Task<ApiResponse<RolePermissionDtoGet>> UpdateRolePermissionAsync(int id, RolePermissionDtoAdd dto);
        Task<ApiResponse<string>> DeleteRolePermissionAsync(int id);
    }
}
