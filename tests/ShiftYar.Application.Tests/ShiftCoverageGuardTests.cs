using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ShiftCoverageGuardTests
{
    [Fact]
    public void ShiftCoverageGuard_FillsEmptyMorningAndEveningSeats()
    {
        var start = new DateTime(2026, 6, 22);
        var end = new DateTime(2026, 7, 5);
        var users = Enumerable.Range(1, 6).Select(MakeUser).ToList();
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 2),
                Shift(2, ShiftLabel.Evening, required: 2),
                Shift(3, ShiftLabel.Night, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true,
                EnforceMaxConsecutiveShifts = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        // فقط شب‌ها پر — صبح و عصر خالی (شبیه خروجی خراب)
        var solution = new ShiftSolution();
        foreach (var day in Enumerable.Range(0, (end - start).Days + 1).Select(i => start.AddDays(i)))
        {
            solution.AddAssignment(1 + (day.Day % 5), 3, day, ShiftLabel.Night, false);
        }

        ShiftCoverageGuard.Enforce(solution, constraints);

        foreach (var day in Enumerable.Range(0, (end - start).Days + 1).Select(i => start.AddDays(i)))
        {
            var mornings = solution.GetShiftAssignments(1, day).Count(a => !a.IsOnCall);
            var evenings = solution.GetShiftAssignments(2, day).Count(a => !a.IsOnCall);
            Assert.True(mornings >= 2, $"Morning understaffed on {day:yyyy-MM-dd}: {mornings}");
            Assert.True(evenings >= 2, $"Evening understaffed on {day:yyyy-MM-dd}: {evenings}");
        }
    }

    [Fact]
    public void StripExcessCoverage_RemovesExtraNightWhenDayAlreadyFilled()
    {
        var start = new DateTime(2026, 7, 1);
        var end = start.AddDays(30); // 31 days
        var users = Enumerable.Range(1, 5).Select(MakeUser).ToList();
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1),
                Shift(2, ShiftLabel.Evening, required: 1),
                Shift(3, ShiftLabel.Night, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        var solution = new ShiftSolution();
        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            solution.AddAssignment(1, 3, day, ShiftLabel.Night, false);
        }

        // یک شب اضافه روی همان روز — مجموع 32 به‌جای 31
        solution.AddAssignment(3, 3, start.AddDays(10), ShiftLabel.Night, false);

        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);

        var totalNights = solution.Assignments.Values
            .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        Assert.Equal(31, totalNights);

        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            var count = solution.GetShiftAssignments(3, day).Count(a => !a.IsOnCall);
            Assert.True(count <= 1, $"Night over capacity on {day:yyyy-MM-dd}: {count}");
        }
    }

    [Fact]
    public void Enforce_NeverExceedsMonthlyNightCapacity()
    {
        var start = new DateTime(2026, 7, 1);
        var end = start.AddDays(30);
        var users = Enumerable.Range(1, 6).Select(MakeUser).ToList();
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1),
                Shift(2, ShiftLabel.Evening, required: 1),
                Shift(3, ShiftLabel.Night, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        var solution = new ShiftSolution();
        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            solution.AddAssignment(1, 3, day, ShiftLabel.Night, false);
        }

        solution.AddAssignment(2, 3, start.AddDays(5), ShiftLabel.Night, false);
        solution.AddAssignment(3, 3, start.AddDays(5), ShiftLabel.Night, false);

        ShiftCoverageGuard.Enforce(solution, constraints);

        var totalNights = solution.Assignments.Values
            .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        Assert.Equal(31, totalNights);
        Assert.Empty(ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints));
    }

    [Fact]
    public void EnforceCapacityCeiling_ResolvesTwoNightsOnSameDayWhenOneIsRequired()
    {
        var start = new DateTime(2026, 7, 1);
        var end = start.AddDays(30);
        var requiredDate = start.AddDays(14);
        var requiredUser = MakeUser(6);
        requiredUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = requiredDate,
            ShiftLabel = ShiftLabel.Night
        });

        var users = Enumerable.Range(1, 5).Select(MakeUser).Append(requiredUser).ToList();
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1),
                Shift(2, ShiftLabel.Evening, required: 1),
                Shift(3, ShiftLabel.Night, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        var solution = new ShiftSolution();
        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            solution.AddAssignment(1, 3, day, ShiftLabel.Night, false);
        }

        solution.AddAssignment(2, 3, requiredDate, ShiftLabel.Night, false);
        ShiftCoverageGuard.EnforceCapacityCeiling(solution, constraints);

        Assert.Equal(1, solution.GetShiftAssignments(3, requiredDate).Count(a => !a.IsOnCall));
        Assert.Contains(
            solution.GetShiftAssignments(3, requiredDate),
            a => a.UserId == 6 && !a.IsOnCall);
        Assert.Equal(31, solution.Assignments.Values.Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall));
        Assert.Empty(ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints));
    }

    [Fact]
    public void EnforceCapacityCeiling_PreservesRequiredNightShift()
    {
        var start = new DateTime(2026, 7, 1);
        var end = start.AddDays(30);
        var requiredUser = MakeUser(6);
        var requiredDate = start.AddDays(14);
        requiredUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = requiredDate,
            ShiftLabel = ShiftLabel.Night
        });

        var users = Enumerable.Range(1, 5).Select(MakeUser).Append(requiredUser).ToList();
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1),
                Shift(2, ShiftLabel.Evening, required: 1),
                Shift(3, ShiftLabel.Night, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        var solution = new ShiftSolution();
        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            solution.AddAssignment(1, 3, day, ShiftLabel.Night, false);
        }

        solution.AddAssignment(2, 3, requiredDate, ShiftLabel.Night, false);
        ApprovedRequestGuard.ForceApply(solution, constraints);
        ShiftCoverageGuard.EnforceCapacityCeiling(solution, constraints);

        Assert.True(
            solution.GetShiftAssignments(3, requiredDate).Any(a => a.UserId == 6 && !a.IsOnCall),
            "Required night shift for user 6 must survive capacity ceiling enforcement.");
        Assert.Equal(1, solution.GetShiftAssignments(3, requiredDate).Count(a => !a.IsOnCall));
        Assert.Equal(31, solution.Assignments.Values.Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall));
    }

    [Fact]
    public void EnforceCapacityCeiling_FixesDuplicateNightOnSameDay()
    {
        var start = new DateTime(2026, 7, 1);
        var end = start.AddDays(30);
        var users = Enumerable.Range(1, 5).Select(MakeUser).ToList();
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1),
                Shift(2, ShiftLabel.Evening, required: 1),
                Shift(3, ShiftLabel.Night, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 }
        };

        var solution = new ShiftSolution();
        var collisionDay = start.AddDays(14);
        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            solution.AddAssignment(1, 3, day, ShiftLabel.Night, false);
        }

        solution.AddAssignment(2, 3, collisionDay, ShiftLabel.Night, false);

        ShiftCoverageGuard.EnforceCapacityCeiling(solution, constraints);

        var totalNights = solution.Assignments.Values
            .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        Assert.Equal(31, totalNights);
        Assert.Equal(1, solution.GetShiftAssignments(3, collisionDay).Count(a => !a.IsOnCall));
        Assert.Empty(ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints));
    }

    [Fact]
    public void GetConflictingRequiredShiftSlotViolations_DetectsTwoOnSameNightSlot()
    {
        var start = new DateTime(2026, 7, 1);
        var date = start.AddDays(14);
        var userA = MakeUser(6);
        var userB = MakeUser(7);
        userA.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = date, ShiftLabel = ShiftLabel.Night });
        userB.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = date, ShiftLabel = ShiftLabel.Night });

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(30),
            UserConstraints = [userA, userB],
            ShiftRequirements = [Shift(3, ShiftLabel.Night, required: 1)]
        };

        var conflicts = ApprovedRequestGuard.GetConflictingRequiredShiftSlotViolations(constraints);
        Assert.Single(conflicts);
    }

    [Fact]
    public void Optimize_DoesNotLeaveMostMorningEveningEmpty()
    {
        var start = new DateTime(2026, 6, 22);
        var users = Enumerable.Range(1, 6).Select(MakeUser).ToList();
        foreach (var u in users)
        {
            u.MaxConsecutiveShifts = 3;
            u.MaxShiftsPerWeek = 5;
        }

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(13),
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1),
                Shift(2, ShiftLabel.Evening, required: 1),
                Shift(3, ShiftLabel.Night, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true,
                EnforceMaxConsecutiveShifts = true,
                EnforceWeeklyMaxShifts = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 },
            SoftWeights = SoftRuleWeights.CreateDefault()
        };

        var solution = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            InitialTemperature = 400,
            FinalTemperature = 0.1,
            CoolingRate = 0.97,
            MaxIterations = 1500,
            MaxIterationsWithoutImprovement = 300,
            PenaltyWeight = 1000
        }).Optimize();

        var days = 14;
        var morningFilled = 0;
        var eveningFilled = 0;
        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            if (solution.GetShiftAssignments(1, day).Any(a => !a.IsOnCall)) morningFilled++;
            if (solution.GetShiftAssignments(2, day).Any(a => !a.IsOnCall)) eveningFilled++;
        }

        Assert.True(morningFilled >= days - 1, $"Morning coverage {morningFilled}/{days}");
        Assert.True(eveningFilled >= days - 1, $"Evening coverage {eveningFilled}/{days}");
    }

    [Fact]
    public void GetUnderCapacityViolations_ReturnsDetailedPersianViolationsWhenShiftIncomplete()
    {
        var start = new DateTime(2026, 9, 8); // 17 Shahrivar 1405
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start,
            UserConstraints = [MakeUser(1), MakeUser(2)],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 10,
                    ShiftLabel = ShiftLabel.Morning,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement
                        {
                            SpecialtyId = 10,
                            SpecialtyName = "پرستار",
                            RequiredTotalCount = 4
                        }
                    ]
                }
            ]
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 10, start, ShiftLabel.Morning, isOnCall: false);

        var violations = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);

        Assert.Single(violations);
        var v = violations[0];
        Assert.Contains("ظرفیت تکمیل نشده در تاریخ شمسی", v);
        Assert.Contains("1405/06/17", v);
        Assert.Contains("2026/09/08", v);
        Assert.Contains("شیفت صبح", v);
        Assert.Contains("پرستار", v);
        Assert.Contains("1 نفر از 4 نفر تخصیص داده شده است", v);
        Assert.Contains("3 نفر کسری", v);
    }

    [Fact]
    public void GetUnderCapacityViolations_ReturnsEmptyWhenAllShiftsFullyFilled()
    {
        var start = new DateTime(2026, 9, 8);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start,
            UserConstraints = [MakeUser(1), MakeUser(2), MakeUser(3), MakeUser(4)],
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 10,
                    ShiftLabel = ShiftLabel.Morning,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement
                        {
                            SpecialtyId = 10,
                            SpecialtyName = "پرستار",
                            RequiredTotalCount = 4
                        }
                    ]
                }
            ]
        };

        var solution = new ShiftSolution();
        for (int i = 1; i <= 4; i++)
        {
            solution.AddAssignment(i, 10, start, ShiftLabel.Morning, isOnCall: false);
        }

        var violations = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);

        Assert.Empty(violations);
    }

    [Fact]
    public void ForceFillAllMissingCoverage_FillsDeficitViaSameDaySwap()
    {
        var date = new DateTime(2026, 9, 9);
        var u1 = MakeUser(1); // On Morning, can work Evening
        var u2 = MakeUser(2); // Can only work Morning (e.g. rested after night on date-2)
        var users = new List<UserConstraint> { u1, u2 };

        var constraints = new ShiftConstraints
        {
            StartDate = date,
            EndDate = date,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1),
                Shift(2, ShiftLabel.Evening, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        // User 2 cannot take Evening due to permissions
        u2.AllowedShiftLabels = [ShiftLabel.Morning];

        // Solution currently has u1 on Morning, Evening is EMPTY
        var solution = new ShiftSolution();
        solution.AddAssignment(u1.UserId, 1, date, ShiftLabel.Morning, isOnCall: false);

        // Before: Evening has deficit
        var beforeUnder = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        Assert.Single(beforeUnder);

        // Run ForceFillAllMissingCoverage
        ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);

        // After: Evening and Morning are both filled (u1 moved to Evening, u2 took Morning)
        var afterUnder = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        Assert.Empty(afterUnder);

        Assert.True(solution.HasAssignment(u1.UserId, 2, date)); // u1 on Evening
        Assert.True(solution.HasAssignment(u2.UserId, 1, date)); // u2 on Morning
    }

    [Fact]
    public void ForceFillAllMissingCoverage_DoesNotViolate_MaxConsecutiveShifts_WhenEnforced()
    {
        var start = new DateTime(2026, 9, 6);
        var end = new DateTime(2026, 9, 9);
        var u1 = MakeUser(1);
        u1.MaxConsecutiveShifts = 3; // Worked 3 days already: 6, 7, 8
        var users = new List<UserConstraint> { u1 };

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMaxConsecutiveShifts = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(u1.UserId, 1, new DateTime(2026, 9, 6), ShiftLabel.Morning, false);
        solution.AddAssignment(u1.UserId, 1, new DateTime(2026, 9, 7), ShiftLabel.Morning, false);
        solution.AddAssignment(u1.UserId, 1, new DateTime(2026, 9, 8), ShiftLabel.Morning, false);
        // Date 2026-09-09 is empty and u1 is at 3 consecutive days

        ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);

        // Must NOT assign u1 to 4th consecutive day
        Assert.False(solution.HasAssignment(u1.UserId, 1, new DateTime(2026, 9, 9)));
        var violations = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        Assert.Single(violations);
    }

    [Fact]
    public void ForceFillAllMissingCoverage_FillsDeficit_WhenQuotaRelaxationNeeded()
    {
        var start = new DateTime(2026, 9, 6);
        var end = new DateTime(2026, 9, 8);
        var u1 = MakeUser(1);
        u1.MaxConsecutiveShifts = 5;
        u1.ExactMorningShiftCount = 1; // Quota is 1
        var users = new List<UserConstraint> { u1 };

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = users,
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, required: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMaxConsecutiveShifts = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(u1.UserId, 1, new DateTime(2026, 9, 6), ShiftLabel.Morning, false);
        // u1 already has 1 morning shift (quota satisfied). Date 2026-09-08 is empty.

        ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);

        var violations = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        Assert.Empty(violations);
        Assert.True(solution.HasAssignment(u1.UserId, 1, new DateTime(2026, 9, 8)));
    }

    [Fact]
    public void UnderCapacityViolations_AreFormattedWithPersianDatesAndDeficits()
    {
        var start = new DateTime(2026, 8, 25);
        var end = new DateTime(2026, 8, 25);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            UserConstraints = [MakeUser(1)],
            ShiftRequirements = [Shift(4, ShiftLabel.Morning, required: 4)],
            HardRules = new HardRuleSet(),
            GlobalConstraints = new GlobalConstraints()
        };

        var solution = new ShiftSolution();
        // Zero assignments for a required shift of 4
        var violations = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("ظرفیت تکمیل نشده") && v.Contains("1405/06/03") && v.Contains("4 نفر کسری"));
    }

    private static UserConstraint MakeUser(int id) => new()
    {
        UserId = id,
        Gender = id % 2 == 0 ? UserGender.Female : UserGender.Male,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        MaxConsecutiveShifts = 3,
        MaxShiftsPerWeek = 5,
        MinDaysBetweenNightShifts = 2,
        MaxNightShiftsPerMonth = 10,
        MinRestDaysBetweenShifts = 0
    };

    private static ShiftRequirement Shift(int id, ShiftLabel label, int required) => new()
    {
        ShiftId = id,
        ShiftLabel = label,
        DurationHours = label == ShiftLabel.Night ? 12 : 6,
        StartTime = TimeSpan.FromHours(8),
        EndTime = TimeSpan.FromHours(14),
        SpecialtyRequirements =
        [
            new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = required }
        ]
    };
}
