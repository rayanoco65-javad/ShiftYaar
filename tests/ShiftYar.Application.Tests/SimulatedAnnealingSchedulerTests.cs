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

        [Fact]
        public void Optimize_RequiresShiftManager_OnEveningShift_WhenConfigured()
        {
            var start = new DateTime(2026, 8, 1);
            var constraints = BuildConstraints(
                start: start,
                days: 5,
                users: new[]
                {
                    User(1, UserGender.Male, canBeShiftManager: false),
                    User(2, UserGender.Male, canBeShiftManager: true),
                },
                specialty: new SpecialtyRequirement
                {
                    SpecialtyId = 10,
                    RequiredTotalCount = 1
                });

            constraints.ShiftRequirements[0].ShiftLabel = ShiftLabel.Evening;
            constraints.GlobalConstraints.RequireManagerForEveningShift = true;

            var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

            foreach (var date in DateRange(constraints))
            {
                var assignments = solution.GetShiftAssignments(1, date).Where(a => !a.IsOnCall).ToList();
                if (assignments.Count == 0)
                {
                    continue;
                }

                Assert.Contains(assignments, a => constraints.UserConstraints.First(u => u.UserId == a.UserId).CanBeShiftManager);
            }
        }

        [Fact]
        public void Optimize_EnforcesApprovedOnShiftRequest_AsHardConstraint()
        {
            var date = new DateTime(2026, 8, 6);
            var constraints = BuildConstraints(
                start: date,
                days: 1,
                users: new[]
                {
                    User(1, UserGender.Male),
                    User(2, UserGender.Male),
                },
                specialty: new SpecialtyRequirement
                {
                    SpecialtyId = 10,
                    RequiredTotalCount = 1
                });

            constraints.UserConstraints[0].RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = date,
                ShiftLabel = ShiftLabel.Morning
            });

            var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

            Assert.True(solution.HasAssignment(1, 1, date));
            Assert.Equal(1, solution.GetShiftAssignments(1, date).Single(a => !a.IsOnCall).UserId);
        }

        [Fact]
        public void Optimize_BlocksUserOnFullDayOffRequest()
        {
            var date = new DateTime(2026, 8, 7);
            var constraints = BuildConstraints(
                start: date,
                days: 1,
                users: new[]
                {
                    User(1, UserGender.Male),
                    User(2, UserGender.Male),
                },
                specialty: new SpecialtyRequirement
                {
                    SpecialtyId = 10,
                    RequiredTotalCount = 1
                });

            constraints.UserConstraints[0].UnavailableDates.Add(date);

            var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

            Assert.False(solution.GetUserAssignments(1, date).Any());
        }

        [Fact]
        public void Optimize_BlocksOnlySpecificShift_OnPartialOffRequest()
        {
            var date = new DateTime(2026, 8, 8);
            var constraints = new ShiftConstraints
            {
                DepartmentId = 1,
                StartDate = date,
                EndDate = date,
                UserConstraints = new List<UserConstraint>
                {
                    User(1, UserGender.Male),
                    User(2, UserGender.Male),
                },
                ShiftRequirements = new List<ShiftRequirement>
                {
                    new()
                    {
                        ShiftId = 1,
                        ShiftLabel = ShiftLabel.Morning,
                        DepartmentId = 1,
                        DurationHours = 8,
                        SpecialtyRequirements = new List<SpecialtyRequirement>
                        {
                            new() { SpecialtyId = 10, RequiredTotalCount = 1 }
                        }
                    },
                    new()
                    {
                        ShiftId = 2,
                        ShiftLabel = ShiftLabel.Evening,
                        DepartmentId = 1,
                        DurationHours = 8,
                        SpecialtyRequirements = new List<SpecialtyRequirement>
                        {
                            new() { SpecialtyId = 10, RequiredTotalCount = 1 }
                        }
                    }
                },
                HardRules = new HardRuleSet
                {
                    ForbidDuplicateDailyAssignments = true,
                    EnforceMaxShiftsPerDay = true,
                    EnforceSpecialtyCapacity = true
                },
                GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
            };

            constraints.UserConstraints[0].UnavailableShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = date,
                ShiftLabel = ShiftLabel.Morning
            });

            var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

            Assert.False(solution.HasAssignment(1, 1, date));
        }

        [Fact]
        public void Optimize_EnforcesApprovedRequests_OverLongPeriod_WithTightConstraints()
        {
            // شبیه‌سازی شرایط واقعی: بازه یک‌ماهه، قیود سخت فعال، چند درخواست تأییدشده
            var start = new DateTime(2026, 8, 1);
            for (int run = 0; run < 5; run++)
            {
                var constraints = BuildConstraints(
                    start: start,
                    days: 30,
                    users: Enumerable.Range(1, 8)
                        .Select(i => User(i, i % 2 == 0 ? UserGender.Female : UserGender.Male, canBeShiftManager: i <= 2))
                        .ToArray(),
                    specialty: new SpecialtyRequirement
                    {
                        SpecialtyId = 10,
                        RequiredTotalCount = 2
                    });

                constraints.HardRules.EnforceMinRestDays = true;
                foreach (var u in constraints.UserConstraints)
                {
                    u.MinRestDaysBetweenShifts = 0;
                    u.MaxConsecutiveShifts = 4;
                }

                var requiredDate = start.AddDays(5); // 6th day
                var offDate = start.AddDays(6);      // 7th day

                constraints.UserConstraints[2].RequiredShiftSlots.Add(new ShiftSlotConstraint
                {
                    Date = requiredDate,
                    ShiftLabel = ShiftLabel.Morning
                });
                constraints.UserConstraints[3].UnavailableDates.Add(offDate);
                constraints.UserConstraints[4].RequiredPresenceDates.Add(requiredDate);

                var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

                // درخواست حضور در شیفت مشخص باید قطعی باشد
                Assert.True(
                    solution.GetShiftAssignments(1, requiredDate).Any(a => a.UserId == 3 && !a.IsOnCall),
                    $"Run {run}: approved on-shift request for user 3 was not honored");

                // درخواست عدم حضور کل روز باید قطعی باشد
                Assert.False(
                    solution.GetUserAssignments(4, offDate).Any(),
                    $"Run {run}: approved off request for user 4 was violated");

                // درخواست حضور کل روز باید قطعی باشد
                Assert.True(
                    solution.GetUserAssignments(5, requiredDate).Any(),
                    $"Run {run}: approved full-day presence request for user 5 was not honored");
            }
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

    private static UserConstraint User(int id, UserGender gender, bool canBeShiftManager = false) => new()
    {
        UserId = id,
        Gender = gender,
        SpecialtyId = 10,
        IsActive = true,
        CanBeShiftManager = canBeShiftManager,
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
