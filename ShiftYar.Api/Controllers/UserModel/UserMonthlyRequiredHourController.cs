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
    /// ساعات موظفی تأییدشده پرسنل به‌تفکیک ماه شمسی و دپارتمان قبل از اجرای شیفت‌بندی.
    /// </summary>
    [Authorize]
    public class UserMonthlyRequiredHourController : BaseController
    {
        private readonly IUserMonthlyRequiredHourService _service;

        public UserMonthlyRequiredHourController(ShiftYarDbContext context, IUserMonthlyRequiredHourService service)
            : base(context)
        {
            _service = service;
        }

        /// <summary>
        /// دریافت پیش‌نمایش متمرکز ساعات موظفی پرسنل یک بخش برای یک ماه شمسی (شامل ساعات محاسباتی سیستم و ساعات قبلاً تأییدشده)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<DepartmentMonthlyRequiredHoursPreviewDto>>> GetDepartmentMonthlyRequiredHoursPreview(
            int departmentId, int persianYear, int persianMonth)
        {
            var result = await _service.GetDepartmentMonthlyRequiredHoursPreviewAsync(departmentId, persianYear, persianMonth);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>
        /// بررسی وضعیت تکمیل ساعات موظفی پرسنل فعال یک بخش برای یک ماه (آیا شیفت‌بندی مجاز است؟)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<DepartmentRequiredHoursStatusDto>>> CheckDepartmentRequiredHoursStatus(
            int departmentId, int persianYear, int persianMonth)
        {
            var result = await _service.CheckDepartmentRequiredHoursStatusAsync(departmentId, persianYear, persianMonth);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>
        /// ذخیره یا به‌روزرسانی گروهی ساعات موظفی پرسنل یک بخش برای یک ماه شمسی
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<List<UserMonthlyRequiredHourDtoGet>>>> UpsertDepartmentMonthlyRequiredHours(
            [FromBody] UserMonthlyRequiredHourBulkUpsertDto dto)
        {
            var result = await _service.BulkUpsertDepartmentRequiredHoursAsync(dto);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>
        /// دریافت لیست ساعات موظفی ذخیره‌شده بر اساس فیلتر
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserMonthlyRequiredHourDtoGet>>>> GetUserMonthlyRequiredHours(
            [FromQuery] UserMonthlyRequiredHourFilter filter)
        {
            var result = await _service.GetRequiredHoursAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// دریافت رکورد ساعت موظفی با شناسه
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserMonthlyRequiredHourDtoGet>>> GetUserMonthlyRequiredHour(int id)
        {
            var result = await _service.GetRequiredHourAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        /// <summary>
        /// دریافت رکورد ساعت موظفی یک کاربر برای سال و ماه مشخص
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserMonthlyRequiredHourDtoGet>>> GetUserMonthlyRequiredHourByUserMonth(
            int userId, int persianYear, int persianMonth)
        {
            var result = await _service.GetRequiredHourByUserMonthAsync(userId, persianYear, persianMonth);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }
    }
}
