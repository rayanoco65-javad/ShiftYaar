using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.DepartmentModel
{
    public interface IDepartmentSchedulingSettingsService
    {
        Task<ApiResponse<PagedResponse<DepartmentSchedulingSettingsDtoGet>>> GetSettingsAsync(DepartmentSchedulingSettingsFilter filter);
        Task<ApiResponse<DepartmentSchedulingSettingsDtoGet>> GetSettingAsync(int id);
        Task<ApiResponse<DepartmentSchedulingSettingsDtoGet>> CreateSettingAsync(DepartmentSchedulingSettingsDtoAdd dto);
        Task<ApiResponse<DepartmentSchedulingSettingsDtoGet>> UpdateSettingAsync(int id, DepartmentSchedulingSettingsDtoAdd dto);
        Task<ApiResponse<string>> DeleteSettingAsync(int id);
    }
}
