using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.UserModel
{
    public interface IUserMonthlyDayShiftQuotaService
    {
        Task<ApiResponse<PagedResponse<UserMonthlyDayShiftQuotaDtoGet>>> GetQuotasAsync(UserMonthlyDayShiftQuotaFilter filter);
        Task<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>> GetQuotaAsync(int id);
        Task<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>> GetQuotaByUserMonthAsync(int userId, int persianYear, int persianMonth);
        Task<ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>> GetDepartmentMonthQuotasAsync(int departmentId, int persianYear, int persianMonth);
        Task<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>> UpsertQuotaAsync(UserMonthlyDayShiftQuotaDtoAdd dto);
        Task<ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>> UpsertBulkAsync(UserMonthlyDayShiftQuotaBulkUpsertDto dto);
        Task<ApiResponse<string>> DeleteQuotaAsync(int id);
    }
}
