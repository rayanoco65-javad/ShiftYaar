using System;
using System.Collections.Generic;
using System.Linq;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Interfaces.ProductivityModel;
using ShiftYar.Domain.Entities.ProductivityModel;

namespace ShiftYar.Application.Features.ProductivityModel.Services
{
    /// <summary>
    /// Implements the formulas mandated by the Regulation of Productivity Promotion of Clinical Employees.
    /// FinalMonthly = (BaseWeekly × Weeks) − (WeeklyReductions × Weeks) − NightHolidayCredit.
    /// NightHolidayCredit = (NightHolidayHours × 1.5) − NightHolidayHours.
    /// </summary>
    public class WorkingHoursCalculator : IWorkingHoursCalculator
    {
        public WorkingHoursCalculationResultDto CalculateMonthlyHours(WorkingHoursCalculationRequestDto request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Staff == null)
            {
                throw new ArgumentException("Staff employment info is required.", nameof(request));
            }

            if (request.NumberOfWeeksInMonth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.NumberOfWeeksInMonth));
            }

            var targetMonth = request.TargetMonth == default
                ? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
                : new DateTime(request.TargetMonth.Year, request.TargetMonth.Month, 1);

            var staffInfo = BuildStaffInfo(request.Staff);
            var ruleConfig = ResolveRuleConfig(request.RuleOverrides);
            var nightHolidayHours = Math.Max(0m, request.NightHolidayHours);
            var yearsOfService = staffInfo.ResolveYearsOfService(targetMonth);

            var seniorityReduction = ruleConfig.GetSeniorityReduction(yearsOfService);
            var hardshipReduction = staffInfo.HasHardshipDuty ? ruleConfig.HardshipReductionPerWeek : 0m;
            var rotatingReduction = staffInfo.HasUncommonRotatingShifts ? ruleConfig.RotatingShiftReductionPerWeek : 0m;

            var totalWeeklyReduction = Math.Min(ruleConfig.MaxWeeklyReduction, seniorityReduction + hardshipReduction + rotatingReduction);
            var weeklyRequiredHours = Math.Max(0m, ruleConfig.BaseWeeklyHours - totalWeeklyReduction);

            var monthlyBase = ruleConfig.BaseWeeklyHours * request.NumberOfWeeksInMonth;
            var monthlyReductionFromWeekly = totalWeeklyReduction * request.NumberOfWeeksInMonth;

            var nightHolidayWeightedHours = nightHolidayHours * ruleConfig.NightHolidayMultiplier;
            var nightHolidayCredit = nightHolidayWeightedHours - nightHolidayHours;

            var totalDeductions = monthlyReductionFromWeekly + nightHolidayCredit;
            var finalMonthlyRequiredHours = Math.Max(0m, monthlyBase - totalDeductions);

            return new WorkingHoursCalculationResultDto
            {
                BaseMonthlyHours = monthlyBase,
                TotalDeductions = totalDeductions,
                FinalMonthlyRequiredHours = finalMonthlyRequiredHours,
                Breakdown = new WorkingHoursCalculationBreakdownDto
                {
                    BaseWeeklyHours = ruleConfig.BaseWeeklyHours,
                    WeeklyRequiredHours = weeklyRequiredHours,
                    SeniorityReductionPerWeek = seniorityReduction,
                    HardshipReductionPerWeek = hardshipReduction,
                    RotatingShiftReductionPerWeek = rotatingReduction,
                    TotalWeeklyReduction = totalWeeklyReduction,
                    MonthlyReductionFromWeeklyAdjustments = monthlyReductionFromWeekly,
                    NightHolidayHoursReported = nightHolidayHours,
                    NightHolidayWeightedHours = nightHolidayWeightedHours,
                    NightHolidayCreditHours = nightHolidayCredit,
                    Notes = new List<string>
                    {
                        "Base weekly hours set to 44 according to national regulation.",
                        "Maximum aggregate weekly reduction capped at 8 hours.",
                        "Night/holiday shifts count with 1.5 multiplier (credit equals extra 0.5 per hour)."
                    }
                }
            };
        }
        private static StaffEmploymentInfo BuildStaffInfo(StaffEmploymentInfoDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentException("Staff employment info is required.");
            }

            return new StaffEmploymentInfo
            {
                StaffId = dto.StaffId,
                StaffFullName = dto.StaffFullName,
                DateOfEmployment = dto.DateOfEmployment,
                YearsOfServiceOverride = dto.YearsOfServiceOverride,
                HasHardshipDuty = dto.HasHardshipDuty,
                HasUncommonRotatingShifts = dto.HasUncommonRotatingShifts
            };
        }

        private static ProductivityRuleConfig ResolveRuleConfig(ProductivityRuleOverrideDto? overrides)
        {
            if (overrides == null)
            {
                return ProductivityRuleConfig.CreateDefault();
            }

            var defaultConfig = ProductivityRuleConfig.CreateDefault();

            var seniorityBands = overrides.SeniorityReductionBands != null && overrides.SeniorityReductionBands.Count > 0
                ? overrides.SeniorityReductionBands
                    .Select(band => new SeniorityReductionBand(band.MinYearsInclusive, band.MaxYearsInclusive, band.ReductionHours))
                    .ToList()
                : defaultConfig.SeniorityReductionBands;

            return new ProductivityRuleConfig
            {
                BaseWeeklyHours = overrides.BaseWeeklyHours ?? defaultConfig.BaseWeeklyHours,
                MaxWeeklyReduction = overrides.MaxWeeklyReduction ?? defaultConfig.MaxWeeklyReduction,
                HardshipReductionPerWeek = overrides.HardshipReductionPerWeek ?? defaultConfig.HardshipReductionPerWeek,
                RotatingShiftReductionPerWeek = overrides.RotatingShiftReductionPerWeek ?? defaultConfig.RotatingShiftReductionPerWeek,
                NightHolidayMultiplier = overrides.NightHolidayMultiplier ?? defaultConfig.NightHolidayMultiplier,
                SeniorityReductionBands = seniorityBands
            };
        }
    }
}

