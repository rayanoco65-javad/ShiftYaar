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
    /// سهمیه صبح/عصر و ترجیح توزیع مازاد هر کاربر برای یک ماه شمسی.
    /// </summary>
    [Authorize]
    public class UserMonthlyDayShiftQuotaController : BaseController
    {
        private readonly IUserMonthlyDayShiftQuotaService _service;

        public UserMonthlyDayShiftQuotaController(ShiftYarDbContext context, IUserMonthlyDayShiftQuotaService service)
            : base(context)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserMonthlyDayShiftQuotaDtoGet>>>> GetUserMonthlyDayShiftQuotas(
            [FromQuery] UserMonthlyDayShiftQuotaFilter filter)
        {
            var result = await _service.GetQuotasAsync(filter);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>>> GetUserMonthlyDayShiftQuota(int id)
        {
            var result = await _service.GetQuotaAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>>> GetUserMonthlyDayShiftQuotaByUserMonth(
            int userId, int persianYear, int persianMonth)
        {
            var result = await _service.GetQuotaByUserMonthAsync(userId, persianYear, persianMonth);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>>> GetDepartmentMonthlyDayShiftQuotas(
            int departmentId, int persianYear, int persianMonth)
        {
            var result = await _service.GetDepartmentMonthQuotasAsync(departmentId, persianYear, persianMonth);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>>> UpsertUserMonthlyDayShiftQuota(
            [FromBody] UserMonthlyDayShiftQuotaDtoAdd dto)
        {
            var result = await _service.UpsertQuotaAsync(dto);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>>> UpsertDepartmentMonthlyDayShiftQuotas(
            [FromBody] UserMonthlyDayShiftQuotaBulkUpsertDto dto)
        {
            var result = await _service.UpsertBulkAsync(dto);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteUserMonthlyDayShiftQuota(int id)
        {
            var result = await _service.DeleteQuotaAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }
    }
}
