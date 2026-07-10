using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class SimulatedAnnealingSchedulerTests
{
    private static SimulatedAnnealingParameters FastParameters => new()
    {
        InitialTemperature = 400,
        FinalTemperature = 0.1,
        CoolingRate = 0.93,
        MaxIterations = 1500,
        MaxIterationsWithoutImprovement = 250,
        PenaltyWeight = 1000
    };

    [Fact]
    public void Optimize_RespectsExplicitGenderMix_ForRegularStaff()
    {
        var constraints = BuildConstraints(
            start: new DateTime(2026, 7, 1),
            days: 7,
            users: new[]
            {
                User(1, UserGender.Male),
                User(2, UserGender.Male),
                User(3, UserGender.Female),
                User(4, UserGender.Female),
            },
            specialty: new SpecialtyRequirement
            {
                SpecialtyId = 10,
                RequiredMaleCount = 1,
                RequiredFemaleCount = 1,
                RequiredTotalCount = 2
            });

        var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

        AssertAllDaysMeetGenderRequirement(solution, constraints, regular: true);
        AssertNoDuplicateDailyAssignments(solution);
    }

    [Fact]
    public void Optimize_SetsOnCallAssignments_WhenConfigured()
    {
        var constraints = BuildConstraints(
            start: new DateTime(2026, 7, 1),
            days: 5,
            users: new[]
            {
                User(1, UserGender.Male),
                User(2, UserGender.Female),
                User(3, UserGender.Male),
                User(4, UserGender.Female),
                User(5, UserGender.Male),
                User(6, UserGender.Female),
            },
            specialty: new SpecialtyRequirement
            {
                SpecialtyId = 10,
                RequiredMaleCount = 1,
                RequiredFemaleCount = 1,
                RequiredTotalCount = 2,
                OnCallMaleCount = 1,
                OnCallFemaleCount = 1,
                OnCallTotalCount = 2
            });

        var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

        AssertAllDaysMeetGenderRequirement(solution, constraints, regular: false);
        AssertOnCallCountPerDay(solution, constraints, expectedPerDay: 2);
        Assert.True(solution.Assignments.Values.Any(a => a.IsOnCall));
    }

    [Fact]
    public void Optimize_AllowsAnyGender_WhenOnlyTotalCountSpecified()
    {
        var constraints = BuildConstraints(
            start: new DateTime(2026, 7, 1),
            days: 5,
            users: new[]
            {
                User(1, UserGender.Male),
                User(2, UserGender.Male),
                User(3, UserGender.Male),
                User(4, UserGender.Female),
            },
            specialty: new SpecialtyRequirement
            {
                SpecialtyId = 10,
                RequiredTotalCount = 2
            });

        var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

        foreach (var date in DateRange(constraints))
        {
            var assignments = solution.GetShiftAssignments(shiftId: 1, date)
                .Where(a => !a.IsOnCall)
                .ToList();

            Assert.Equal(2, assignments.Count);
        }
    }

    [Fact]
    public void Optimize_DoesNotRewardUnderstaffing_WhenEnoughPersonnelExist()
    {
        var constraints = BuildConstraints(
            start: new DateTime(2026, 7, 1),
            days: 7,
            users: Enumerable.Range(1, 6).Select(i => User(i, i % 2 == 0 ? UserGender.Female : UserGender.Male)).ToArray(),
            specialty: new SpecialtyRequirement
            {
                SpecialtyId = 10,
                RequiredMaleCount = 1,
                RequiredFemaleCount = 1,
                RequiredTotalCount = 2,
                OnCallTotalCount = 1
            });

        var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

        var staffedDays = 0;
        foreach (var date in DateRange(constraints))
        {
            var regular = CountBySpecialty(solution, constraints, 1, date, 10, isOnCall: false);
            var onCall = CountBySpecialty(solution, constraints, 1, date, 10, isOnCall: true);
            if (regular >= 2 && onCall >= 1)
            {
                staffedDays++;
            }
        }

        Assert.True(staffedDays >= 5, $"Expected most days fully staffed, got {staffedDays}/7");
    }

    [Fact]
    public void Optimize_NoUserAssignedTwiceOnSameDay()
    {
        var constraints = BuildConstraints(
            start: new DateTime(2026, 7, 1),
            days: 14,
            users: Enumerable.Range(1, 8).Select(i => User(i, i % 2 == 0 ? UserGender.Female : UserGender.Male)).ToArray(),
            specialty: new SpecialtyRequirement
            {
                SpecialtyId = 10,
                RequiredMaleCount = 1,
                RequiredFemaleCount = 1,
                RequiredTotalCount = 2
            });

        var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

        AssertNoDuplicateDailyAssignments(solution);
    }

    private static ShiftConstraints BuildConstraints(
        DateTime start,
        int days,
        UserConstraint[] users,
        SpecialtyRequirement specialty)
    {
        return new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            UserConstraints = users.ToList(),
            ShiftRequirements = new List<ShiftRequirement>
            {
                new()
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 1,
                    DurationHours = 8,
                    SpecialtyRequirements = new List<SpecialtyRequirement> { specialty }
                }
            },
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = false,
                EnforceMaxConsecutiveShifts = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints
            {
                MaxShiftsPerDay = 1,
                PreferSpecialtyMatch = true,
                RequireGenderBalance = true
            }
        };
    }

    private static UserConstraint User(int id, UserGender gender) => new()
    {
        UserId = id,
        Gender = gender,
        SpecialtyId = 10,
        IsActive = true,
        MaxConsecutiveShifts = 7,
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 7
    };

    private static IEnumerable<DateTime> DateRange(ShiftConstraints constraints)
    {
        for (var d = constraints.StartDate.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
        {
            yield return d;
        }
    }

    private static int CountBySpecialty(ShiftSolution solution, ShiftConstraints constraints, int shiftId, DateTime date, int specialtyId, bool isOnCall)
    {
        return solution.GetShiftAssignments(shiftId, date)
            .Count(a => a.IsOnCall == isOnCall &&
                        constraints.UserConstraints.First(u => u.UserId == a.UserId).SpecialtyId == specialtyId);
    }

    private static UserGender GetGender(ShiftConstraints constraints, int userId) =>
        constraints.UserConstraints.First(u => u.UserId == userId).Gender;

    private static void AssertAllDaysMeetGenderRequirement(ShiftSolution solution, ShiftConstraints constraints, bool regular)
    {
        var specialty = constraints.ShiftRequirements[0].SpecialtyRequirements[0];

        foreach (var date in DateRange(constraints))
        {
            var assignments = solution.GetShiftAssignments(1, date)
                .Where(a => a.IsOnCall == !regular)
                .ToList();

            if (regular)
            {
                if (specialty.RequiredMaleCount > 0)
                {
                    Assert.Equal(specialty.RequiredMaleCount, assignments.Count(a => GetGender(constraints, a.UserId) == UserGender.Male));
                }

                if (specialty.RequiredFemaleCount > 0)
                {
                    Assert.Equal(specialty.RequiredFemaleCount, assignments.Count(a => GetGender(constraints, a.UserId) == UserGender.Female));
                }
            }
            else
            {
                if (specialty.OnCallMaleCount > 0)
                {
                    Assert.Equal(specialty.OnCallMaleCount, assignments.Count(a => GetGender(constraints, a.UserId) == UserGender.Male));
                }

                if (specialty.OnCallFemaleCount > 0)
                {
                    Assert.Equal(specialty.OnCallFemaleCount, assignments.Count(a => GetGender(constraints, a.UserId) == UserGender.Female));
                }
            }
        }
    }

    private static void AssertOnCallCountPerDay(ShiftSolution solution, ShiftConstraints constraints, int expectedPerDay)
    {
        foreach (var date in DateRange(constraints))
        {
            var onCallCount = solution.GetShiftAssignments(1, date).Count(a => a.IsOnCall);
            Assert.Equal(expectedPerDay, onCallCount);
        }
    }

    private static void AssertNoDuplicateDailyAssignments(ShiftSolution solution)
    {
        var duplicates = solution.Assignments.Values
            .GroupBy(a => new { a.UserId, Date = a.Date.Date })
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }
}
