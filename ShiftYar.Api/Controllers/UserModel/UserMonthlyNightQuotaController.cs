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
    /// سهمیه حداقل شیفت شب / شب تعطیل هر کاربر برای یک ماه شمسی (قبل از شیفت‌بندی).
    /// </summary>
    [Authorize]
    public class UserMonthlyNightQuotaController : BaseController
    {
        private readonly IUserMonthlyNightQuotaService _service;

        public UserMonthlyNightQuotaController(ShiftYarDbContext context, IUserMonthlyNightQuotaService service)
            : base(context)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserMonthlyNightQuotaDtoGet>>>> GetUserMonthlyNightQuotas(
            [FromQuery] UserMonthlyNightQuotaFilter filter)
        {
            var result = await _service.GetQuotasAsync(filter);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserMonthlyNightQuotaDtoGet>>> GetUserMonthlyNightQuota(int id)
        {
            var result = await _service.GetQuotaAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserMonthlyNightQuotaDtoGet>>> GetUserMonthlyNightQuotaByUserMonth(
            int userId, int persianYear, int persianMonth)
        {
            var result = await _service.GetQuotaByUserMonthAsync(userId, persianYear, persianMonth);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserMonthlyNightQuotaDtoGet>>>> GetDepartmentMonthlyNightQuotas(
            int departmentId, int persianYear, int persianMonth)
        {
            var result = await _service.GetDepartmentMonthQuotasAsync(departmentId, persianYear, persianMonth);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>ایجاد یا به‌روزرسانی سهمیه یک کاربر برای یک ماه شمسی</summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserMonthlyNightQuotaDtoGet>>> UpsertUserMonthlyNightQuota(
            [FromBody] UserMonthlyNightQuotaDtoAdd dto)
        {
            var result = await _service.UpsertQuotaAsync(dto);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>تنظیم یک‌جای سهمیه شب کاربران یک دپارتمان برای ماه شمسی</summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<List<UserMonthlyNightQuotaDtoGet>>>> UpsertDepartmentMonthlyNightQuotas(
            [FromBody] UserMonthlyNightQuotaBulkUpsertDto dto)
        {
            var result = await _service.UpsertBulkAsync(dto);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteUserMonthlyNightQuota(int id)
        {
            var result = await _service.DeleteQuotaAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }
    }
}
