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
        /// اعمال تنظیمات پیش‌فرض بهینه زمان‌بندی برای دپارتمان (ایجاد یا بازنویسی).
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<DepartmentSchedulingSettingsDtoGet>>> ApplyDefaultDepartmentSchedulingSettings(int departmentId)
        {
            var result = await _service.ApplyDefaultSettingsAsync(departmentId);
            if (!result.IsSuccess)
            {
                return result.Message.Contains("یافت نشد") ? NotFound(result) : BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// تنظیمات توزیع شیفت‌های شب بر اساس سابقه (سازگاری با کلاینت‌های قبلی).
        /// </summary>
        [HttpPut("night-shift-distribution/{id}")]
        public async Task<ActionResult<ApiResponse<DepartmentSchedulingSettingsDtoGet>>> UpdateNightShiftDistributionSettings(
            int id,
            [FromBody] NightShiftDistributionSettingsDto dto)
        {
            return await UpdateShiftSeniorityDistributionSettings(id, new ShiftSeniorityDistributionSettingsDto
            {
                EnableNightShiftDistributionBySeniority = dto.EnableNightShiftDistributionBySeniority,
                NightShiftDistributionType = dto.NightShiftDistributionType,
                NightShiftDistributionWeight = dto.NightShiftDistributionWeight,
                SeniorityDistributionSlope = dto.SeniorityDistributionSlope
            });
        }

        /// <summary>
        /// تنظیمات توزیع صبح/عصر/شب بر اساس سابقه (تفکیکی).
        /// </summary>
        [HttpPut("shift-seniority-distribution/{id}")]
        public async Task<ActionResult<ApiResponse<DepartmentSchedulingSettingsDtoGet>>> UpdateShiftSeniorityDistributionSettings(
            int id,
            [FromBody] ShiftSeniorityDistributionSettingsDto dto)
        {
            var currentSettings = await _service.GetSettingAsync(id);
            if (!currentSettings.IsSuccess) return NotFound(currentSettings);

            var updatedSettings = currentSettings.Data;
            var dtoAdd = ToDtoAdd(updatedSettings);

            if (dto.EnableMorningShiftDistributionBySeniority.HasValue)
                dtoAdd.EnableMorningShiftDistributionBySeniority = dto.EnableMorningShiftDistributionBySeniority;
            if (dto.MorningShiftDistributionType.HasValue)
                dtoAdd.MorningShiftDistributionType = dto.MorningShiftDistributionType;
            if (dto.MorningShiftDistributionWeight.HasValue)
                dtoAdd.MorningShiftDistributionWeight = dto.MorningShiftDistributionWeight;

            if (dto.EnableEveningShiftDistributionBySeniority.HasValue)
                dtoAdd.EnableEveningShiftDistributionBySeniority = dto.EnableEveningShiftDistributionBySeniority;
            if (dto.EveningShiftDistributionType.HasValue)
                dtoAdd.EveningShiftDistributionType = dto.EveningShiftDistributionType;
            if (dto.EveningShiftDistributionWeight.HasValue)
                dtoAdd.EveningShiftDistributionWeight = dto.EveningShiftDistributionWeight;

            if (dto.EnableNightShiftDistributionBySeniority.HasValue)
                dtoAdd.EnableNightShiftDistributionBySeniority = dto.EnableNightShiftDistributionBySeniority;
            if (dto.NightShiftDistributionType.HasValue)
                dtoAdd.NightShiftDistributionType = dto.NightShiftDistributionType;
            if (dto.NightShiftDistributionWeight.HasValue)
                dtoAdd.NightShiftDistributionWeight = dto.NightShiftDistributionWeight;
            if (dto.SeniorityDistributionSlope.HasValue)
                dtoAdd.SeniorityDistributionSlope = dto.SeniorityDistributionSlope;

            var result = await _service.UpdateSettingAsync(id, dtoAdd);
            if (!result.IsSuccess) return BadRequest(result);
            return Ok(result);
        }

        private static DepartmentSchedulingSettingsDtoAdd ToDtoAdd(DepartmentSchedulingSettingsDtoGet s) => new()
        {
            DepartmentId = s.DepartmentId,
            ForbidDuplicateDailyAssignments = s.ForbidDuplicateDailyAssignments,
            EnforceMaxShiftsPerDay = s.EnforceMaxShiftsPerDay,
            EnforceMinRestDays = s.EnforceMinRestDays,
            EnforceMaxConsecutiveShifts = s.EnforceMaxConsecutiveShifts,
            EnforceWeeklyMaxShifts = s.EnforceWeeklyMaxShifts,
            EnforceNightShiftMonthlyCap = s.EnforceNightShiftMonthlyCap,
            EnforceSpecialtyCapacity = s.EnforceSpecialtyCapacity,
            MinRestDaysBetweenShifts = s.MinRestDaysBetweenShifts,
            MaxConsecutiveShifts = s.MaxConsecutiveShifts,
            MaxShiftsPerWeek = s.MaxShiftsPerWeek,
            MaxNightShiftsPerMonth = s.MaxNightShiftsPerMonth,
            MaxShiftsPerDay = s.MaxShiftsPerDay,
            MaxConsecutiveNightShifts = s.MaxConsecutiveNightShifts,
            GenderBalanceWeight = s.GenderBalanceWeight,
            SpecialtyPreferenceWeight = s.SpecialtyPreferenceWeight,
            UserUnwantedShiftWeight = s.UserUnwantedShiftWeight,
            UserPreferredShiftWeight = s.UserPreferredShiftWeight,
            WeeklyMaxWeight = s.WeeklyMaxWeight,
            MonthlyNightCapWeight = s.MonthlyNightCapWeight,
            FairShiftCountBalanceWeight = s.FairShiftCountBalanceWeight,
            ExtraShiftRotationWeight = s.ExtraShiftRotationWeight,
            ShiftLabelBalanceWeight = s.ShiftLabelBalanceWeight,
            FairnessLookbackMonths = s.FairnessLookbackMonths,
            EnforceMinimumShiftsForRotatingStaff = s.EnforceMinimumShiftsForRotatingStaff,
            MinMorningShiftsForThreeShiftRotation = s.MinMorningShiftsForThreeShiftRotation,
            MinEveningShiftsForThreeShiftRotation = s.MinEveningShiftsForThreeShiftRotation,
            MinNightShiftsForThreeShiftRotation = s.MinNightShiftsForThreeShiftRotation,
            MinFirstShiftForTwoShiftRotation = s.MinFirstShiftForTwoShiftRotation,
            MinSecondShiftForTwoShiftRotation = s.MinSecondShiftForTwoShiftRotation,
            EnableNightShiftPreference = s.EnableNightShiftPreference,
            NightShiftPreferenceType = s.NightShiftPreferenceType,
            NightShiftPreferenceWeight = s.NightShiftPreferenceWeight,
            RequireManagerForEveningShift = s.RequireManagerForEveningShift,
            RequireManagerForNightShift = s.RequireManagerForNightShift,
            ShiftManagerRequirementWeight = s.ShiftManagerRequirementWeight,
            EnableMorningShiftDistributionBySeniority = s.EnableMorningShiftDistributionBySeniority,
            MorningShiftDistributionType = s.MorningShiftDistributionType,
            MorningShiftDistributionWeight = s.MorningShiftDistributionWeight,
            EnableEveningShiftDistributionBySeniority = s.EnableEveningShiftDistributionBySeniority,
            EveningShiftDistributionType = s.EveningShiftDistributionType,
            EveningShiftDistributionWeight = s.EveningShiftDistributionWeight,
            EnableNightShiftDistributionBySeniority = s.EnableNightShiftDistributionBySeniority,
            NightShiftDistributionType = s.NightShiftDistributionType,
            NightShiftDistributionWeight = s.NightShiftDistributionWeight,
            SeniorityDistributionSlope = s.SeniorityDistributionSlope,
            AllowCurrentMonthScheduling = s.AllowCurrentMonthScheduling,
            AllowMonthlyRescheduleWithAutoDelete = s.AllowMonthlyRescheduleWithAutoDelete,
            AllowEveningAfterNightShift = s.AllowEveningAfterNightShift,
            AllowNightShiftAfterNightShift = s.AllowNightShiftAfterNightShift
        };
    }

    /// <summary>
    /// DTO سازگاری برای تنظیمات فقط-شب.
    /// </summary>
    public class NightShiftDistributionSettingsDto
    {
        public bool? EnableNightShiftDistributionBySeniority { get; set; }
        public int? NightShiftDistributionType { get; set; }
        public double? NightShiftDistributionWeight { get; set; }
        public double? SeniorityDistributionSlope { get; set; }
    }

    /// <summary>
    /// DTO تنظیمات توزیع صبح/عصر/شب بر اساس سابقه.
    /// نوع: 0=اولویت سابقه بیشتر، 1=اولویت سابقه کمتر، 2=خنثی.
    /// </summary>
    public class ShiftSeniorityDistributionSettingsDto
    {
        public bool? EnableMorningShiftDistributionBySeniority { get; set; }
        public int? MorningShiftDistributionType { get; set; }
        public double? MorningShiftDistributionWeight { get; set; }

        public bool? EnableEveningShiftDistributionBySeniority { get; set; }
        public int? EveningShiftDistributionType { get; set; }
        public double? EveningShiftDistributionWeight { get; set; }

        public bool? EnableNightShiftDistributionBySeniority { get; set; }
        public int? NightShiftDistributionType { get; set; }
        public double? NightShiftDistributionWeight { get; set; }

        public double? SeniorityDistributionSlope { get; set; }
    }
}
