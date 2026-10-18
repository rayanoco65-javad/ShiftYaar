using System;
using System.Collections.Generic;
using System.Linq;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ZeroHolidayCapacityShiftTests
{
    [Fact]
    public void Optimize_DepartmentWithZeroHolidayNightCapacity_DoesNotViolateCapacityOrAssignOnHolidays()
    {
        // 30-day month with 5 holidays (Fridays: day 1, 8, 15, 22, 29)
        var start = new DateTime(2026, 10, 23);
        var end = new DateTime(2026, 11, 21);
        var holidays = new HashSet<DateTime>
        {
            new(2026, 10, 23),
            new(2026, 10, 30),
            new(2026, 11, 6),
            new(2026, 11, 13),
            new(2026, 11, 20)
        };

        var quotas = new[] { 1, 3, 3, 3, 2, 2, 3, 3, 1, 1, 1, 1, 1 }; // sum = 24, within 25 normal days

        var users = quotas.Select((q, idx) => new UserConstraint
        {
            UserId = idx + 1,
            UserName = $"User_{idx + 1}",
            SpecialtyId = 3,
            ShiftType = ShiftTypes.RotatingShift,
            IsActive = true,
            ExactNightShiftCount = q
        }).ToList();

        var nightShift = new ShiftRequirement
        {
            ShiftId = 9,
            ShiftLabel = ShiftLabel.Night,
            SpecialtyRequirements =
            [
                new SpecialtyRequirement
                {
                    SpecialtyId = 3,
                    RequiredTotalCount = 1,
                    HolidayRequiredTotalCount = 0
                }
            ]
        };

        var morningShift = new ShiftRequirement
        {
            ShiftId = 7,
            ShiftLabel = ShiftLabel.Morning,
            SpecialtyRequirements =
            [
                new SpecialtyRequirement
                {
                    SpecialtyId = 3,
                    RequiredTotalCount = 3,
                    HolidayRequiredTotalCount = 2
                }
            ]
        };

        var constraints = new ShiftConstraints
        {
            DepartmentId = 3,
            StartDate = start,
            EndDate = end,
            HolidayDates = holidays,
            UserConstraints = users,
            ShiftRequirements = [morningShift, nightShift]
        };

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1500,
            MaxIterationsWithoutImprovement = 300
        });

        var solution = scheduler.Optimize();

        // Must not have any over-capacity violations
        var overCapacityViolations = ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints);
        Assert.Empty(overCapacityViolations);

        // Must not have any night assignments on holidays (capacity is 0)
        foreach (var holiday in holidays)
        {
            var holidayNights = solution.GetShiftAssignments(nightShift.ShiftId, holiday).Where(a => !a.IsOnCall).ToList();
            Assert.Empty(holidayNights);
        }
    }
}
