using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.UserModel
{
    public interface IUserMonthlyNightQuotaService
    {
        Task<ApiResponse<PagedResponse<UserMonthlyNightQuotaDtoGet>>> GetQuotasAsync(UserMonthlyNightQuotaFilter filter);
        Task<ApiResponse<UserMonthlyNightQuotaDtoGet>> GetQuotaAsync(int id);
        Task<ApiResponse<UserMonthlyNightQuotaDtoGet>> GetQuotaByUserMonthAsync(int userId, int persianYear, int persianMonth);
        Task<ApiResponse<List<UserMonthlyNightQuotaDtoGet>>> GetDepartmentMonthQuotasAsync(int departmentId, int persianYear, int persianMonth);
        Task<ApiResponse<UserMonthlyNightQuotaDtoGet>> UpsertQuotaAsync(UserMonthlyNightQuotaDtoAdd dto);
        Task<ApiResponse<List<UserMonthlyNightQuotaDtoGet>>> UpsertBulkAsync(UserMonthlyNightQuotaBulkUpsertDto dto);
        Task<ApiResponse<string>> DeleteQuotaAsync(int id);
    }
}
