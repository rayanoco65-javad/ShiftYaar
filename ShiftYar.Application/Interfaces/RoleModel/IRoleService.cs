using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Application.Features.RoleModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.RoleModel
{
    public interface IRoleService
    {
        Task<ApiResponse<PagedResponse<RoleDtoGet>>> GetFilteredRolesAsync(RoleFilter filter);
        Task<ApiResponse<RoleDtoGet>> GetByIdAsync(int id);
        Task<ApiResponse<RoleDtoGet>> CreateAsync(RoleDtoAdd dto);
        Task<ApiResponse<RoleDtoGet>> UpdateAsync(int id, RoleDtoAdd dto);
        Task<ApiResponse<string>> DeleteAsync(int id);
    }
}
