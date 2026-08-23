using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.DepartmentModel;
using Xunit;

namespace ShiftYar.Application.Tests;

public class DepartmentPostNightShiftRulesApplierTests
{
    [Fact]
    public void Apply_WhenNightAfterNightAllowed_RaisesMaxConsecutiveNightShiftsToTwo()
    {
        var constraints = new ShiftConstraints();
        var settings = new DepartmentSchedulingSettings
        {
            AllowEveningAfterNightShift = true,
            AllowNightShiftAfterNightShift = true,
            MaxConsecutiveNightShifts = 1
        };

        DepartmentPostNightShiftRulesApplier.Apply(constraints, settings);

        Assert.True(constraints.HardRules.AllowNightShiftAfterNightShift);
        Assert.True(constraints.GlobalConstraints.AllowConsecutiveNightShifts);
        Assert.Equal(2, constraints.GlobalConstraints.MaxConsecutiveNightShifts);
    }

    [Fact]
    public void Apply_WhenNightAfterNightAllowed_ClearsUserNightSpacingGap()
    {
        var constraints = new ShiftConstraints
        {
            UserConstraints =
            [
                new UserConstraint { UserId = 1, MinDaysBetweenNightShifts = 2 },
                new UserConstraint { UserId = 2, MinDaysBetweenNightShifts = 2 }
            ]
        };
        var settings = new DepartmentSchedulingSettings
        {
            AllowNightShiftAfterNightShift = true,
            MaxConsecutiveNightShifts = 2
        };

        DepartmentPostNightShiftRulesApplier.Apply(constraints, settings);

        Assert.All(constraints.UserConstraints, u => Assert.Equal(0, u.MinDaysBetweenNightShifts));
    }

    [Fact]
    public void Apply_WhenNightAfterNightDisabled_KeepsMaxConsecutiveNightShifts()
    {
        var constraints = new ShiftConstraints();
        var settings = new DepartmentSchedulingSettings
        {
            AllowEveningAfterNightShift = true,
            AllowNightShiftAfterNightShift = false,
            MaxConsecutiveNightShifts = 1
        };

        DepartmentPostNightShiftRulesApplier.Apply(constraints, settings);

        Assert.False(constraints.HardRules.AllowNightShiftAfterNightShift);
        Assert.False(constraints.GlobalConstraints.AllowConsecutiveNightShifts);
        Assert.Equal(1, constraints.GlobalConstraints.MaxConsecutiveNightShifts);
    }
}
