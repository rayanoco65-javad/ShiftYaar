using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ShiftSeniorityDistributionTests
{
    [Fact]
    public void Optimize_PrefersSeniorStaffForMorningWhenTypeZeroEnabled()
    {
        var start = new DateTime(2026, 9, 1);
        var senior = MakeUser(1, experienceYears: 20);
        var junior = MakeUser(2, experienceYears: 1);
        var filler = MakeUser(3, experienceYears: 10);

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(13),
            UserConstraints = [senior, junior, filler],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 1,
                    DurationHours = 7,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                }
            ],
            HardRules = new HardRuleSet
            {
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true,
                ForbidDuplicateDailyAssignments = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            SoftWeights = SoftRuleWeights.CreateDefault(),
            EnableMorningShiftDistributionBySeniority = true,
            MorningShiftDistributionType = 0,
            SeniorityDistributionSlope = 1.0
        };
        constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight = 8.0;
        constraints.SoftWeights.FairShiftCountBalanceWeight = 0.1;
        constraints.SoftWeights.FairWorkedHoursBalanceWeight = 0.1;
        constraints.SoftWeights.ExtraShiftRotationWeight = 0.1;

        var solution = new SimulatedAnnealingScheduler(
            constraints,
            new SimulatedAnnealingParameters
            {
                InitialTemperature = 300,
                FinalTemperature = 0.05,
                CoolingRate = 0.94,
                MaxIterations = 2500,
                MaxIterationsWithoutImprovement = 400,
                PenaltyWeight = 1000
            }).Optimize();

        var seniorMornings = CountLabel(solution, senior.UserId, ShiftLabel.Morning);
        var juniorMornings = CountLabel(solution, junior.UserId, ShiftLabel.Morning);
        Assert.True(
            seniorMornings >= juniorMornings,
            $"Expected senior mornings >= junior; got senior={seniorMornings}, junior={juniorMornings}");
    }

    [Fact]
    public void Optimize_PrefersJuniorStaffForEveningWhenTypeOneEnabled()
    {
        var start = new DateTime(2026, 9, 1);
        var senior = MakeUser(11, experienceYears: 20);
        var junior = MakeUser(12, experienceYears: 1);
        var filler = MakeUser(13, experienceYears: 10);

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(13),
            UserConstraints = [senior, junior, filler],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 2,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 1,
                    DurationHours = 7,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                }
            ],
            HardRules = new HardRuleSet
            {
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true,
                ForbidDuplicateDailyAssignments = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            SoftWeights = SoftRuleWeights.CreateDefault(),
            EnableEveningShiftDistributionBySeniority = true,
            EveningShiftDistributionType = 1,
            SeniorityDistributionSlope = 1.0
        };
        constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight = 8.0;
        constraints.SoftWeights.FairShiftCountBalanceWeight = 0.1;
        constraints.SoftWeights.FairWorkedHoursBalanceWeight = 0.1;
        constraints.SoftWeights.ExtraShiftRotationWeight = 0.1;

        var solution = new SimulatedAnnealingScheduler(
            constraints,
            new SimulatedAnnealingParameters
            {
                InitialTemperature = 300,
                FinalTemperature = 0.05,
                CoolingRate = 0.94,
                MaxIterations = 2500,
                MaxIterationsWithoutImprovement = 400,
                PenaltyWeight = 1000
            }).Optimize();

        var seniorEvenings = CountLabel(solution, senior.UserId, ShiftLabel.Evening);
        var juniorEvenings = CountLabel(solution, junior.UserId, ShiftLabel.Evening);
        Assert.True(
            juniorEvenings >= seniorEvenings,
            $"Expected junior evenings >= senior; got junior={juniorEvenings}, senior={seniorEvenings}");
    }

    [Fact]
    public void Guard_RebalancesEqualSharesWhenTypeNeutral()
    {
        var start = new DateTime(2026, 9, 1);
        var senior = MakeUser(1, experienceYears: 20);
        var junior = MakeUser(2, experienceYears: 1);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(9),
            UserConstraints = [senior, junior],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 1,
                    DurationHours = 7,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                },
                new ShiftRequirement
                {
                    ShiftId = 2,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 1,
                    DurationHours = 7,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                }
            ],
            HardRules = new HardRuleSet
            {
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            EnableMorningShiftDistributionBySeniority = true,
            MorningShiftDistributionType = 2,
            EnableEveningShiftDistributionBySeniority = true,
            EveningShiftDistributionType = 2,
            SoftWeights = SoftRuleWeights.CreateDefault()
        };
        constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight = 2;
        constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight = 2;

        var solution = new ShiftSolution();
        for (var i = 0; i < 8; i++)
        {
            solution.AddAssignment(senior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        for (var i = 8; i < 10; i++)
        {
            solution.AddAssignment(junior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        ShiftSeniorityDistributionGuard.Enforce(solution, constraints);

        var seniorCount = CountDay(solution, senior.UserId);
        var juniorCount = CountDay(solution, junior.UserId);
        Assert.Equal(10, seniorCount + juniorCount);
        Assert.True(Math.Abs(seniorCount - juniorCount) <= 1, $"senior={seniorCount}, junior={juniorCount}");
    }

    [Fact]
    public void Guard_TypeOne_MovesDayShiftsFromSeniorToJunior()
    {
        var start = new DateTime(2026, 9, 1);
        var senior = MakeUser(1, experienceYears: 13);
        var junior = MakeUser(2, experienceYears: 1);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(13),
            UserConstraints = [senior, junior],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 1,
                    DurationHours = 7,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                },
                new ShiftRequirement
                {
                    ShiftId = 2,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 1,
                    DurationHours = 7,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                }
            ],
            HardRules = new HardRuleSet
            {
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            EnableMorningShiftDistributionBySeniority = true,
            MorningShiftDistributionType = 1,
            EnableEveningShiftDistributionBySeniority = true,
            EveningShiftDistributionType = 1,
            SoftWeights = SoftRuleWeights.CreateDefault()
        };
        constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight = 2;
        constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight = 2;

        var solution = new ShiftSolution();
        for (var i = 0; i < 11; i++)
        {
            solution.AddAssignment(senior.UserId, i % 2 == 0 ? 1 : 2, start.AddDays(i),
                i % 2 == 0 ? ShiftLabel.Morning : ShiftLabel.Evening, false);
        }

        for (var i = 11; i < 14; i++)
        {
            solution.AddAssignment(junior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        ShiftSeniorityDistributionGuard.Enforce(solution, constraints);

        var seniorCount = CountDay(solution, senior.UserId);
        var juniorCount = CountDay(solution, junior.UserId);
        Assert.True(juniorCount >= seniorCount,
            $"Type=1 should favor junior day shifts; senior={seniorCount}, junior={juniorCount}");
    }

    private static int CountDay(ShiftSolution solution, int userId) =>
        solution.GetUserAllAssignments(userId)
            .Count(a => !a.IsOnCall
                        && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening));

    private static int CountLabel(ShiftSolution solution, int userId, ShiftLabel label) =>
        solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == label && !a.IsOnCall);

    private static UserConstraint MakeUser(int id, int experienceYears) => new()
    {
        UserId = id,
        UserName = $"u{id}",
        Gender = id % 2 == 0 ? UserGender.Female : UserGender.Male,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        ExperienceYears = experienceYears,
        MaxConsecutiveShifts = 30,
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 7,
        MinDaysBetweenNightShifts = 1
    };
}
