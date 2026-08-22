using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.UserModel
{
    public interface IUserMonthlyComboShiftQuotaService
    {
        Task<ApiResponse<PagedResponse<UserMonthlyComboShiftQuotaDtoGet>>> GetQuotasAsync(UserMonthlyComboShiftQuotaFilter filter);
        Task<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>> GetQuotaAsync(int id);
        Task<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>> GetQuotaByUserMonthAsync(int userId, int persianYear, int persianMonth);
        Task<ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>> GetDepartmentMonthQuotasAsync(int departmentId, int persianYear, int persianMonth);
        Task<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>> UpsertQuotaAsync(UserMonthlyComboShiftQuotaDtoAdd dto);
        Task<ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>> UpsertBulkAsync(UserMonthlyComboShiftQuotaBulkUpsertDto dto);
        Task<ApiResponse<string>> DeleteQuotaAsync(int id);
    }
}
