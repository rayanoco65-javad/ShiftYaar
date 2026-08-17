using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class ApprovedOffNightBeforeRulesTests
{
    private static readonly List<Shift> DepartmentShifts =
    [
        new() { Id = 1, Label = ShiftLabel.Morning, StartTime = TimeSpan.FromHours(7) },
        new() { Id = 2, Label = ShiftLabel.Evening, StartTime = TimeSpan.FromHours(14) },
        new() { Id = 3, Label = ShiftLabel.Night, StartTime = TimeSpan.FromHours(20) }
    ];

    [Fact]
    public void ApplyNightBeforeOffConstraint_AddsNightSlotOnPreviousDay_ForFullDayOff()
    {
        var user = new UserConstraint { UserId = 1 };
        var offDate = new DateTime(2026, 8, 10);

        ApprovedOffNightBeforeRules.ApplyNightBeforeOffConstraint(user, offDate, DepartmentShifts);

        Assert.Contains(user.UnavailableShiftSlots, s =>
            s.Date.Date == offDate.AddDays(-1).Date && s.ShiftLabel == ShiftLabel.Night);
    }

    [Fact]
    public void RequiresNightBeforeBlock_TrueForMorningOff_FalseForEveningOff()
    {
        Assert.True(ApprovedOffNightBeforeRules.RequiresNightBeforeBlock(
            RequestType.SpecificShift, ShiftLabel.Morning));
        Assert.True(ApprovedOffNightBeforeRules.RequiresNightBeforeBlock(
            RequestType.FullDay, ShiftLabel.Morning));
        Assert.False(ApprovedOffNightBeforeRules.RequiresNightBeforeBlock(
            RequestType.SpecificShift, ShiftLabel.Evening));
    }

    [Fact]
    public void ForceApply_RemovesNightShiftOnDayBeforeApprovedMorningOff()
    {
        var monday = new DateTime(2026, 8, 10);
        var sunday = monday.AddDays(-1);
        var user = new UserConstraint
        {
            UserId = 1,
            UserName = "Test",
            SpecialtyId = 10
        };
        user.UnavailableShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = monday,
            ShiftLabel = ShiftLabel.Morning
        });
        ApprovedOffNightBeforeRules.ApplyNightBeforeOffConstraint(user, monday, DepartmentShifts);

        var constraints = new ShiftConstraints
        {
            StartDate = sunday,
            EndDate = monday,
            UserConstraints = [user],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 3,
                    ShiftLabel = ShiftLabel.Night,
                    SpecialtyRequirements = [new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }]
                }
            ]
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 3, sunday, ShiftLabel.Night, isOnCall: false);

        ApprovedRequestGuard.ForceApply(solution, constraints);

        Assert.False(solution.HasAssignment(1, 3, sunday));
        Assert.True(ApprovedOffNightBeforeRules.IsNightBlockedByApprovedOff(user, sunday));
    }
}
