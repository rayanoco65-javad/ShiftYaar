using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class HolidayStaffingRequirementTests
{
    [Fact]
    public void SpecialtyRequirement_ForDay_FallsBackToWeekday_WhenHolidayNull()
    {
        var req = new SpecialtyRequirement
        {
            RequiredMaleCount = 2,
            RequiredFemaleCount = 1,
            RequiredTotalCount = 3,
            OnCallTotalCount = 1
        };

        var holiday = req.ForDay(isHoliday: true);
        Assert.Equal(2, holiday.RequiredMaleCount);
        Assert.Equal(1, holiday.RequiredFemaleCount);
        Assert.Equal(3, holiday.RequiredTotalCount);
        Assert.Equal(1, holiday.OnCallTotalCount);
    }

    [Fact]
    public void SpecialtyRequirement_ForDay_UsesHolidayOverrides()
    {
        var req = new SpecialtyRequirement
        {
            RequiredMaleCount = 2,
            RequiredFemaleCount = 1,
            RequiredTotalCount = 3,
            HolidayRequiredMaleCount = 1,
            HolidayRequiredFemaleCount = 1,
            HolidayRequiredTotalCount = 2
        };

        var weekday = req.ForDay(false);
        var holiday = req.ForDay(true);

        Assert.Equal(3, weekday.RequiredTotalCount);
        Assert.Equal(2, holiday.RequiredTotalCount);
        Assert.Equal(1, holiday.RequiredMaleCount);
        Assert.Equal(1, holiday.RequiredFemaleCount);
    }

    [Fact]
    public void Optimize_UsesLowerHolidayStaffingCounts()
    {
        var start = new DateTime(2026, 8, 23);
        var holiday = start.AddDays(2);

        var users = Enumerable.Range(1, 6).Select(i => new UserConstraint
        {
            UserId = i,
            Gender = i % 2 == 0 ? UserGender.Female : UserGender.Male,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            AllowedShiftLabels = [ShiftLabel.Morning],
            MaxConsecutiveShifts = 14,
            MinRestDaysBetweenShifts = 0,
            MaxShiftsPerWeek = 7
        }).ToList();

        var specialty = new SpecialtyRequirement
        {
            SpecialtyId = 10,
            RequiredMaleCount = 2,
            RequiredFemaleCount = 1,
            RequiredTotalCount = 3,
            HolidayRequiredMaleCount = 1,
            HolidayRequiredFemaleCount = 1,
            HolidayRequiredTotalCount = 2
        };

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(4),
            HolidayDates = [holiday.Date],
            UserConstraints = users,
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 1,
                    DurationHours = 8,
                    SpecialtyRequirements = [specialty]
                }
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = false,
                EnforceMaxConsecutiveShifts = false,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var parameters = new SimulatedAnnealingParameters
        {
            InitialTemperature = 400,
            FinalTemperature = 0.1,
            CoolingRate = 0.93,
            MaxIterations = 2000,
            MaxIterationsWithoutImprovement = 300,
            PenaltyWeight = 1000
        };

        for (var run = 0; run < 3; run++)
        {
            var solution = new SimulatedAnnealingScheduler(constraints, parameters).Optimize();

            for (var d = start.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
            {
                var regular = solution.GetShiftAssignments(1, d).Where(a => !a.IsOnCall).ToList();
                var expected = constraints.IsHoliday(d) ? 2 : 3;
                Assert.True(
                    regular.Count <= expected,
                    $"Run {run}: {d:yyyy-MM-dd} has {regular.Count} regulars but capacity is {expected}");

                if (constraints.IsHoliday(d))
                {
                    Assert.Equal(1, regular.Count(a => users.First(u => u.UserId == a.UserId).Gender == UserGender.Male));
                    Assert.Equal(1, regular.Count(a => users.First(u => u.UserId == a.UserId).Gender == UserGender.Female));
                }
            }
        }
    }
}
