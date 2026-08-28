using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class MaxConsecutiveWorkdayRulesTests
{
    [Fact]
    public void WouldExceed_BlocksFourthConsecutiveWorkday_WhenMaxIsThree()
    {
        var start = new DateTime(2026, 8, 23);
        var user = MakeUser(1);
        user.MaxConsecutiveShifts = 3;

        var solution = new ShiftSolution();
        for (var i = 0; i < 3; i++)
        {
            solution.AddAssignment(user.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        var constraints = BuildConstraints(start, user);

        Assert.True(MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
            solution, constraints, user, start.AddDays(3)));
        Assert.False(MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
            solution, constraints, user, start.AddDays(5)));
    }

    [Fact]
    public void FillMissingCoverage_DoesNotAssignFourthConsecutiveWorkday()
    {
        var start = new DateTime(2026, 8, 23);
        var user = MakeUser(1);
        user.MaxConsecutiveShifts = 3;

        var filler = MakeUser(2);
        filler.MaxConsecutiveShifts = 3;

        var solution = new ShiftSolution();
        for (var i = 0; i < 3; i++)
        {
            solution.AddAssignment(user.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(6),
            UserConstraints = [user, filler],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                }
            ],
            HardRules = new HardRuleSet
            {
                EnforceMaxConsecutiveShifts = true,
                EnforceMaxShiftsPerDay = true,
                ForbidDuplicateDailyAssignments = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        ShiftCoverageGuard.Enforce(solution, constraints);

        Assert.False(solution.HasAssignment(user.UserId, 1, start.AddDays(3)));
        Assert.True(solution.GetShiftAssignments(1, start.AddDays(3)).Any(a => a.UserId == filler.UserId));
    }

    [Fact]
    public void OffSpreadPenalty_IgnoresApprovedFullDayOffDays()
    {
        var start = new DateTime(2026, 8, 23);
        var user = MakeUser(1);
        user.UnavailableDates.AddRange(Enumerable.Range(0, 6).Select(i => start.AddDays(i + 1)));

        var solution = new ShiftSolution();
        solution.AddAssignment(user.UserId, 1, start, ShiftLabel.Morning, false);
        solution.AddAssignment(user.UserId, 1, start.AddDays(7), ShiftLabel.Morning, false);

        var constraints = BuildConstraints(start.AddDays(-1), user, days: 10);

        var penaltyAllOff = MaxConsecutiveWorkdayRules.CalculateOffSpreadPenalty(solution, constraints);
        Assert.Equal(0, penaltyAllOff);

        user.UnavailableDates.Clear();
        var penaltyNoApproved = MaxConsecutiveWorkdayRules.CalculateOffSpreadPenalty(solution, constraints);
        Assert.True(penaltyNoApproved > 0);
    }

    [Fact]
    public void ApprovedOn_IsNotBlockedByConsecutiveCap_InForceApply()
    {
        var start = new DateTime(2026, 8, 23);
        var user = MakeUser(1);
        user.MaxConsecutiveShifts = 2;
        for (var i = 0; i < 2; i++)
        {
            user.RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = start.AddDays(i),
                ShiftLabel = ShiftLabel.Evening,
                ShiftId = 2
            });
        }
        user.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = start.AddDays(2),
            ShiftLabel = ShiftLabel.Evening,
            ShiftId = 2
        });

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(6),
            UserConstraints = [user],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 2,
                    ShiftLabel = ShiftLabel.Evening,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                }
            ],
            HardRules = new HardRuleSet { EnforceMaxConsecutiveShifts = true, EnforceMaxShiftsPerDay = true },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new ShiftSolution();
        ApprovedRequestGuard.ForceApply(solution, constraints);

        Assert.Equal(3, solution.GetUserAllAssignments(user.UserId).Count(a => !a.IsOnCall));
    }

    [Fact]
    public void MaxConsecutiveWorkdayGuard_BreaksElevenDayRun_IntoChunksOfThree()
    {
        var start = new DateTime(2026, 8, 28);
        var user = MakeUser(13);
        user.MaxConsecutiveShifts = 3;
        var filler = MakeUser(99);
        filler.MaxConsecutiveShifts = 3;

        var solution = new ShiftSolution();
        for (var i = 0; i < 11; i++)
        {
            solution.AddAssignment(user.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(10),
            UserConstraints = [user, filler],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                }
            ],
            HardRules = new HardRuleSet
            {
                EnforceMaxConsecutiveShifts = true,
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);

        Assert.True(
            MaxConsecutiveWorkdayRules.GetMaxConsecutiveWorkRun(solution, user.UserId) <= 3,
            $"User 13 run={MaxConsecutiveWorkdayRules.GetMaxConsecutiveWorkRun(solution, user.UserId)}");
    }

    private static ShiftConstraints BuildConstraints(DateTime start, UserConstraint user, int days = 14) =>
        new()
        {
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            UserConstraints = [user],
            HardRules = new HardRuleSet { EnforceMaxConsecutiveShifts = true },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

    private static UserConstraint MakeUser(int id) => new()
    {
        UserId = id,
        Gender = UserGender.Female,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        MaxConsecutiveShifts = 3,
        MaxShiftsPerWeek = 6,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night]
    };
}
