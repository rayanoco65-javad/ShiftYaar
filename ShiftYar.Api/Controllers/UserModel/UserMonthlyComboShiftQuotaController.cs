using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Interfaces.UserModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Api.Controllers.UserModel
{
    /// <summary>
    /// سهمیه ترکیبی صبح/عصر و صبح/شب برای پرسنل دو‌نوبته (ماه شمسی).
    /// </summary>
    [Authorize]
    public class UserMonthlyComboShiftQuotaController : BaseController
    {
        private readonly IUserMonthlyComboShiftQuotaService _service;

        public UserMonthlyComboShiftQuotaController(ShiftYarDbContext context, IUserMonthlyComboShiftQuotaService service)
            : base(context)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserMonthlyComboShiftQuotaDtoGet>>>> GetUserMonthlyComboShiftQuotas(
            [FromQuery] UserMonthlyComboShiftQuotaFilter filter)
        {
            return Ok(await _service.GetQuotasAsync(filter));
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>>> GetUserMonthlyComboShiftQuota(int id)
        {
            var result = await _service.GetQuotaAsync(id);
            return result.IsSuccess ? Ok(result) : NotFound(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>>> GetUserMonthlyComboShiftQuotaByUserMonth(
            int userId, int persianYear, int persianMonth)
        {
            var result = await _service.GetQuotaByUserMonthAsync(userId, persianYear, persianMonth);
            return result.IsSuccess ? Ok(result) : NotFound(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>>> GetDepartmentMonthlyComboShiftQuotas(
            int departmentId, int persianYear, int persianMonth)
        {
            var result = await _service.GetDepartmentMonthQuotasAsync(departmentId, persianYear, persianMonth);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>>> UpsertUserMonthlyComboShiftQuota(
            [FromBody] UserMonthlyComboShiftQuotaDtoAdd dto)
        {
            var result = await _service.UpsertQuotaAsync(dto);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>>> UpsertDepartmentMonthlyComboShiftQuotas(
            [FromBody] UserMonthlyComboShiftQuotaBulkUpsertDto dto)
        {
            var result = await _service.UpsertBulkAsync(dto);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteUserMonthlyComboShiftQuota(int id)
        {
            var result = await _service.DeleteQuotaAsync(id);
            return result.IsSuccess ? Ok(result) : NotFound(result);
        }
    }
}
