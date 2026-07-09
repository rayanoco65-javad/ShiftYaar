using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.Settings;
using ShiftYar.Application.Features.Settings.Filters;

namespace ShiftYar.Application.Interfaces.Settings
{
    public interface IAlgorithmSettingsService
    {
        Task<ApiResponse<PagedResponse<AlgorithmSettingsDtoGet>>> GetSettingsAsync(AlgorithmSettingsFilter filter);
        Task<ApiResponse<AlgorithmSettingsDtoGet>> GetSettingAsync(int id);
        Task<ApiResponse<AlgorithmSettingsDtoGet>> GetSettingByDepartmentAndTypeAsync(int? departmentId, int algorithmType);
        Task<ApiResponse<AlgorithmSettingsDtoGet>> CreateSettingAsync(AlgorithmSettingsDtoAdd dto);
        Task<ApiResponse<AlgorithmSettingsDtoGet>> UpdateSettingAsync(int id, AlgorithmSettingsDtoAdd dto);
        Task<ApiResponse<string>> DeleteSettingAsync(int id);
    }
}
