using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using ShiftYar.Application.Interfaces.DepartmentModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.DepartmentModel
{
    [Authorize]
    public class DepartmentSchedulingSettingsController : BaseController
    {
        private readonly IDepartmentSchedulingSettingsService _service;
        public DepartmentSchedulingSettingsController(ShiftYarDbContext context, IDepartmentSchedulingSettingsService service) : base(context)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<DepartmentSchedulingSettingsDtoGet>>>> GetSettings([FromQuery] DepartmentSchedulingSettingsFilter filter)
        {
            var result = await _service.GetSettingsAsync(filter);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<DepartmentSchedulingSettingsDtoGet>>> GetSetting(int id)
        {
            var result = await _service.GetSettingAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<DepartmentSchedulingSettingsDtoGet>>> CreateSetting([FromBody] DepartmentSchedulingSettingsDtoAdd dto)
        {
            var result = await _service.CreateSettingAsync(dto);
            if (!result.IsSuccess) return BadRequest(result);
            return CreatedAtAction(nameof(GetSetting), new { id = result.Data.Id }, result);
        }

        [HttpPut]
        public async Task<ActionResult<ApiResponse<DepartmentSchedulingSettingsDtoGet>>> UpdateSetting(int id, [FromBody] DepartmentSchedulingSettingsDtoAdd dto)
        {
            var result = await _service.UpdateSettingAsync(id, dto);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteSetting(int id)
        {
            var result = await _service.DeleteSettingAsync(id);
            if (!result.IsSuccess) return NotFound(result);
            return Ok(result);
        }

        /// <summary>
        /// تنظیمات توزیع شیفت‌های شب بر اساس سابقه
        /// </summary>
        [HttpPut("night-shift-distribution/{id}")]
        public async Task<ActionResult<ApiResponse<DepartmentSchedulingSettingsDtoGet>>> UpdateNightShiftDistributionSettings(
            int id, 
            [FromBody] NightShiftDistributionSettingsDto dto)
        {
            // دریافت تنظیمات فعلی
            var currentSettings = await _service.GetSettingAsync(id);
            if (!currentSettings.IsSuccess) return NotFound(currentSettings);

            // به‌روزرسانی فیلدهای مربوط به توزیع شیفت‌های شب
            var updatedSettings = currentSettings.Data;
            updatedSettings.EnableNightShiftDistributionBySeniority = dto.EnableNightShiftDistributionBySeniority;
            updatedSettings.NightShiftDistributionType = dto.NightShiftDistributionType;
            updatedSettings.NightShiftDistributionWeight = dto.NightShiftDistributionWeight;
            updatedSettings.SeniorityDistributionSlope = dto.SeniorityDistributionSlope;

            // تبدیل به DTO اضافه و به‌روزرسانی
            var dtoAdd = new DepartmentSchedulingSettingsDtoAdd
            {
                DepartmentId = updatedSettings.DepartmentId,
                ForbidDuplicateDailyAssignments = updatedSettings.ForbidDuplicateDailyAssignments,
                EnforceMaxShiftsPerDay = updatedSettings.EnforceMaxShiftsPerDay,
                EnforceMinRestDays = updatedSettings.EnforceMinRestDays,
                EnforceMaxConsecutiveShifts = updatedSettings.EnforceMaxConsecutiveShifts,
                EnforceWeeklyMaxShifts = updatedSettings.EnforceWeeklyMaxShifts,
                EnforceNightShiftMonthlyCap = updatedSettings.EnforceNightShiftMonthlyCap,
                EnforceSpecialtyCapacity = updatedSettings.EnforceSpecialtyCapacity,
                MinRestDaysBetweenShifts = updatedSettings.MinRestDaysBetweenShifts,
                MaxConsecutiveShifts = updatedSettings.MaxConsecutiveShifts,
                MaxShiftsPerWeek = updatedSettings.MaxShiftsPerWeek,
                MaxNightShiftsPerMonth = updatedSettings.MaxNightShiftsPerMonth,
                MaxShiftsPerDay = updatedSettings.MaxShiftsPerDay,
                MaxConsecutiveNightShifts = updatedSettings.MaxConsecutiveNightShifts,
                GenderBalanceWeight = updatedSettings.GenderBalanceWeight,
                SpecialtyPreferenceWeight = updatedSettings.SpecialtyPreferenceWeight,
                UserUnwantedShiftWeight = updatedSettings.UserUnwantedShiftWeight,
                UserPreferredShiftWeight = updatedSettings.UserPreferredShiftWeight,
                WeeklyMaxWeight = updatedSettings.WeeklyMaxWeight,
                MonthlyNightCapWeight = updatedSettings.MonthlyNightCapWeight,
                FairShiftCountBalanceWeight = updatedSettings.FairShiftCountBalanceWeight,
                ExtraShiftRotationWeight = updatedSettings.ExtraShiftRotationWeight,
                ShiftLabelBalanceWeight = updatedSettings.ShiftLabelBalanceWeight,
                FairnessLookbackMonths = updatedSettings.FairnessLookbackMonths,
                EnforceMinimumShiftsForRotatingStaff = updatedSettings.EnforceMinimumShiftsForRotatingStaff,
                MinMorningShiftsForThreeShiftRotation = updatedSettings.MinMorningShiftsForThreeShiftRotation,
                MinEveningShiftsForThreeShiftRotation = updatedSettings.MinEveningShiftsForThreeShiftRotation,
                MinNightShiftsForThreeShiftRotation = updatedSettings.MinNightShiftsForThreeShiftRotation,
                MinFirstShiftForTwoShiftRotation = updatedSettings.MinFirstShiftForTwoShiftRotation,
                MinSecondShiftForTwoShiftRotation = updatedSettings.MinSecondShiftForTwoShiftRotation,
                EnableNightShiftPreference = updatedSettings.EnableNightShiftPreference,
                NightShiftPreferenceType = updatedSettings.NightShiftPreferenceType,
                NightShiftPreferenceWeight = updatedSettings.NightShiftPreferenceWeight,
                RequireManagerForEveningShift = updatedSettings.RequireManagerForEveningShift,
                RequireManagerForNightShift = updatedSettings.RequireManagerForNightShift,
                ShiftManagerRequirementWeight = updatedSettings.ShiftManagerRequirementWeight,
                // فیلدهای جدید توزیع شیفت‌های شب
                EnableNightShiftDistributionBySeniority = dto.EnableNightShiftDistributionBySeniority,
                NightShiftDistributionType = dto.NightShiftDistributionType,
                NightShiftDistributionWeight = dto.NightShiftDistributionWeight,
                SeniorityDistributionSlope = dto.SeniorityDistributionSlope
            };

            var result = await _service.UpdateSettingAsync(id, dtoAdd);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }
    }

    /// <summary>
    /// DTO برای تنظیمات توزیع شیفت‌های شب بر اساس سابقه
    /// </summary>
    public class NightShiftDistributionSettingsDto
    {
        public bool? EnableNightShiftDistributionBySeniority { get; set; }
        public int? NightShiftDistributionType { get; set; } // 0=شب‌دوست، 1=شب‌گریز، 2=خنثی
        public double? NightShiftDistributionWeight { get; set; }
        public double? SeniorityDistributionSlope { get; set; }
    }
}
