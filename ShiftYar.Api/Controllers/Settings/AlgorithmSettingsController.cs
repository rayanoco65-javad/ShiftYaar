using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Constants;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.Settings;
using ShiftYar.Application.Features.Settings.Filters;
using ShiftYar.Application.Interfaces.Settings;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace ShiftYar.Api.Controllers.Settings
{
    [Authorize]
    [Route("api/[controller]")]
    public class AlgorithmSettingsController : BaseController
    {
        private readonly IAlgorithmSettingsService _service;
        private readonly ILogger<AlgorithmSettingsController> _logger;

        public AlgorithmSettingsController(ShiftYarDbContext context, IAlgorithmSettingsService service, ILogger<AlgorithmSettingsController> logger) : base(context)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// دریافت لیست تنظیمات الگوریتم‌ها
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<AlgorithmSettingsDtoGet>>>> GetSettings([FromQuery] AlgorithmSettingsFilter filter)
        {
            // Logging برای دیباگ
            var userId = User.FindFirstValue(JwtClaimTypes.Sub);
            var roles = User.Claims
                .Where(c => c.Type == JwtClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            _logger.LogInformation("GetSettings called - UserId: {UserId}, Roles: {Roles}", userId, string.Join(", ", roles));

            var result = await _service.GetSettingsAsync(filter);
            return Ok(result);
        }
        //public async Task<ActionResult<ApiResponse<PagedResponse<AlgorithmSettingsDtoGet>>>> GetSettings([FromQuery] AlgorithmSettingsFilter filter)
        //{
        //    // Logging برای دیباگ
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var roles = User.Claims
        //        .Where(c => c.Type == ClaimTypes.Role)
        //        .Select(c => c.Value)
        //        .ToList();

        //    _logger.LogInformation("GetSettings called - UserId: {UserId}, Roles: {Roles}", userId, string.Join(", ", roles));

        //    var result = await _service.GetSettingsAsync(filter);
        //    return Ok(result);
        //}

        /// <summary>
        /// دریافت تنظیمات الگوریتم بر اساس شناسه
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<AlgorithmSettingsDtoGet>>> GetSetting(int id)
        {
            var result = await _service.GetSettingAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        /// <summary>
        /// دریافت تنظیمات الگوریتم بر اساس دپارتمان و نوع الگوریتم
        /// </summary>
        [HttpGet("by-department-and-type")]
        public async Task<ActionResult<ApiResponse<AlgorithmSettingsDtoGet>>> GetSettingByDepartmentAndType(
            [FromQuery] int? departmentId,
            [FromQuery] int algorithmType)
        {
            // Logging برای دیباگ
            var userId = User.FindFirstValue(JwtClaimTypes.Sub);
            var roles = User.Claims
                .Where(c => c.Type == JwtClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            _logger.LogInformation("GetSettingByDepartmentAndType called - UserId: {UserId}, Roles: {Roles}, DepartmentId: {DepartmentId}, AlgorithmType: {AlgorithmType}",
                userId, string.Join(", ", roles), departmentId, algorithmType);

            var result = await _service.GetSettingByDepartmentAndTypeAsync(departmentId, algorithmType);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }
        //public async Task<ActionResult<ApiResponse<AlgorithmSettingsDtoGet>>> GetSettingByDepartmentAndType(
        //    [FromQuery] int? departmentId, 
        //    [FromQuery] int algorithmType)
        //{
        //    // Logging برای دیباگ
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var roles = User.Claims
        //        .Where(c => c.Type == ClaimTypes.Role)
        //        .Select(c => c.Value)
        //        .ToList();

        //    _logger.LogInformation("GetSettingByDepartmentAndType called - UserId: {UserId}, Roles: {Roles}, DepartmentId: {DepartmentId}, AlgorithmType: {AlgorithmType}", 
        //        userId, string.Join(", ", roles), departmentId, algorithmType);

        //    var result = await _service.GetSettingByDepartmentAndTypeAsync(departmentId, algorithmType);
        //    if (!result.IsSuccess) return NotFound(result);
        //    return Ok(result);
        //}


        /// <summary>
        /// ایجاد تنظیمات جدید الگوریتم
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<AlgorithmSettingsDtoGet>>> CreateSetting([FromBody] AlgorithmSettingsDtoAdd dto)
        {
            // Logging برای دیباگ
            var userId = User.FindFirstValue(JwtClaimTypes.Sub);
            var roles = User.Claims
                .Where(c => c.Type == JwtClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            _logger.LogInformation("CreateSetting called - UserId: {UserId}, Roles: {Roles}", userId, string.Join(", ", roles));

            var result = await _service.CreateSettingAsync(dto);
            if (!result.IsSuccess) return BadRequest(result);
            return CreatedAtAction(nameof(GetSetting), new { id = result.Data.Id }, result);
        }
        //public async Task<ActionResult<ApiResponse<AlgorithmSettingsDtoGet>>> CreateSetting([FromBody] AlgorithmSettingsDtoAdd dto)
        //{
        //    // Logging برای دیباگ
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var roles = User.Claims
        //        .Where(c => c.Type == ClaimTypes.Role)
        //        .Select(c => c.Value)
        //        .ToList();

        //    _logger.LogInformation("CreateSetting called - UserId: {UserId}, Roles: {Roles}", userId, string.Join(", ", roles));

        //    var result = await _service.CreateSettingAsync(dto);
        //    if (!result.IsSuccess) return BadRequest(result);
        //    return CreatedAtAction(nameof(GetSetting), new { id = result.Data.Id }, result);
        //}


        /// <summary>
        /// ویرایش تنظیمات الگوریتم
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<AlgorithmSettingsDtoGet>>> UpdateSetting(int id, [FromBody] AlgorithmSettingsDtoAdd dto)
        {
            var result = await _service.UpdateSettingAsync(id, dto);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }


        /// <summary>
        /// حذف تنظیمات الگوریتم
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> DeleteSetting(int id)
        {
            var result = await _service.DeleteSettingAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }
    }
}
