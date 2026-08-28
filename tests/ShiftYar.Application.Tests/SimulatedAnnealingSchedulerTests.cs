using ShiftYar.Application.Common.Utilities;
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
            constraints.ShiftRequirements[0].ManagerRequiredCount = 1;
            constraints.ShiftRequirements[0].ManagerMinLevel1Count = 0;

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
        public void Optimize_FixedMorningUser_NeverGetsEveningOrNight()
        {
            var start = new DateTime(2026, 8, 23);
            var fixedMorning = User(10, UserGender.Female);
            fixedMorning.ShiftType = ShiftTypes.FixedShift;
            fixedMorning.ShiftSubType = ShiftSubTypes.FixedMorning;
            fixedMorning.AllowedShiftLabels = new List<ShiftLabel> { ShiftLabel.Morning };

            var rotating = User(11, UserGender.Male);
            rotating.ShiftType = ShiftTypes.RotatingShift;
            rotating.ShiftSubType = ShiftSubTypes.ThreeShifts;
            rotating.AllowedShiftLabels = new List<ShiftLabel>
            {
                ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night
            };

            var rotating2 = User(12, UserGender.Female);
            rotating2.ShiftType = ShiftTypes.RotatingShift;
            rotating2.ShiftSubType = ShiftSubTypes.ThreeShifts;
            rotating2.AllowedShiftLabels = new List<ShiftLabel>
            {
                ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night
            };

            var rotating3 = User(13, UserGender.Male);
            rotating3.ShiftType = ShiftTypes.RotatingShift;
            rotating3.ShiftSubType = ShiftSubTypes.ThreeShifts;
            rotating3.AllowedShiftLabels = new List<ShiftLabel>
            {
                ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night
            };

            var specialty = new SpecialtyRequirement
            {
                SpecialtyId = 10,
                RequiredTotalCount = 1,
                RequiredMaleCount = 0,
                RequiredFemaleCount = 0
            };

            var constraints = new ShiftConstraints
            {
                DepartmentId = 1,
                StartDate = start,
                EndDate = start.AddDays(6),
                UserConstraints = new List<UserConstraint> { fixedMorning, rotating, rotating2, rotating3 },
                ShiftRequirements = new List<ShiftRequirement>
                {
                    new()
                    {
                        ShiftId = 1,
                        ShiftLabel = ShiftLabel.Morning,
                        DepartmentId = 1,
                        DurationHours = 8,
                        SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
                    },
                    new()
                    {
                        ShiftId = 2,
                        ShiftLabel = ShiftLabel.Evening,
                        DepartmentId = 1,
                        DurationHours = 8,
                        SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
                    },
                    new()
                    {
                        ShiftId = 3,
                        ShiftLabel = ShiftLabel.Night,
                        DepartmentId = 1,
                        DurationHours = 8,
                        SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
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

            for (int run = 0; run < 5; run++)
            {
                var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

                var minaAssignments = solution.GetUserAllAssignments(10);
                Assert.All(minaAssignments, a => Assert.Equal(ShiftLabel.Morning, a.ShiftLabel));
                Assert.Empty(ShiftEligibilityGuard.GetViolations(solution, constraints));
            }
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

        [Fact]
        public void Optimize_HonorsLeaveAndRequiredSlots_AcrossMorningEveningNight_WithOnCall()
        {
            // سناریوی واقعی شبیه مهدی رضایی: مرخصی کل‌روز + حضور اجباری در صبح/شب/عصر
            // در حضور ظرفیت آنکال (که قبلاً می‌توانست کاربر مرخص را برای Morning/Evening پر کند)
            var start = new DateTime(2026, 8, 25); // ~3 شهریور ۱۴۰۵
            var leaveDate = start;                 // 3 شهریور
            var morningRequired = start.AddDays(2); // 5 شهریور
            var nightRequired = start.AddDays(4);   // 7 شهریور
            var eveningRequired = start.AddDays(28); // 31 شهریور

            var users = Enumerable.Range(1, 10)
                .Select(i => User(i, i % 2 == 0 ? UserGender.Female : UserGender.Male, canBeShiftManager: i == 1))
                .ToArray();

            var specialty = new SpecialtyRequirement
            {
                SpecialtyId = 10,
                RequiredMaleCount = 1,
                RequiredFemaleCount = 1,
                RequiredTotalCount = 2,
                OnCallMaleCount = 1,
                OnCallFemaleCount = 1,
                OnCallTotalCount = 2
            };

            var constraints = new ShiftConstraints
            {
                DepartmentId = 1,
                StartDate = start,
                EndDate = eveningRequired,
                UserConstraints = users.ToList(),
                ShiftRequirements = new List<ShiftRequirement>
                {
                    new()
                    {
                        ShiftId = 1,
                        ShiftLabel = ShiftLabel.Morning,
                        DepartmentId = 1,
                        DurationHours = 8,
                        SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
                    },
                    new()
                    {
                        ShiftId = 2,
                        ShiftLabel = ShiftLabel.Evening,
                        DepartmentId = 1,
                        DurationHours = 8,
                        SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
                    },
                    new()
                    {
                        ShiftId = 3,
                        ShiftLabel = ShiftLabel.Night,
                        DepartmentId = 1,
                        DurationHours = 12,
                        ManagerRequiredCount = 1,
                        ManagerMinLevel1Count = 0,
                        SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
                    }
                },
                HardRules = new HardRuleSet
                {
                    ForbidDuplicateDailyAssignments = true,
                    EnforceMaxShiftsPerDay = true,
                    EnforceSpecialtyCapacity = true
                },
                GlobalConstraints = new GlobalConstraints
                {
                    MaxShiftsPerDay = 1
                }
            };

            var mehdi = constraints.UserConstraints[0]; // user 1, CanBeShiftManager
            mehdi.UnavailableDates.Add(leaveDate);
            mehdi.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = morningRequired, ShiftLabel = ShiftLabel.Morning });
            mehdi.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = nightRequired, ShiftLabel = ShiftLabel.Night });
            mehdi.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = eveningRequired, ShiftLabel = ShiftLabel.Evening });

            for (int run = 0; run < 5; run++)
            {
                var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

                Assert.False(
                    solution.GetUserAssignments(1, leaveDate).Any(),
                    $"Run {run}: user on full-day leave still assigned (including OnCall)");

                Assert.True(
                    solution.GetShiftAssignments(1, morningRequired).Any(a => a.UserId == 1 && !a.IsOnCall),
                    $"Run {run}: required Morning assignment missing");

                Assert.True(
                    solution.GetShiftAssignments(3, nightRequired).Any(a => a.UserId == 1 && !a.IsOnCall),
                    $"Run {run}: required Night assignment missing");

                Assert.True(
                    solution.GetShiftAssignments(2, eveningRequired).Any(a => a.UserId == 1 && !a.IsOnCall),
                    $"Run {run}: required Evening assignment missing");

                Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
            }
        }

        [Fact]
        public void ApprovedRequestGuard_ForceApply_AlwaysHonorsRequests_EvenFromEmptySolution()
        {
            var day = new DateTime(2026, 8, 25);
            var constraints = BuildConstraints(
                start: day,
                days: 3,
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
                    RequiredTotalCount = 2,
                    OnCallTotalCount = 1
                });

            // سه شیفت
            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 2,
                ShiftLabel = ShiftLabel.Evening,
                DepartmentId = 1,
                DurationHours = 8,
                SpecialtyRequirements = new List<SpecialtyRequirement>
                {
                    new() { SpecialtyId = 10, RequiredTotalCount = 2, OnCallTotalCount = 1 }
                }
            });

            var leaveDate = day;
            var requiredMorning = day.AddDays(1);
            constraints.UserConstraints[0].UnavailableDates.Add(leaveDate);
            constraints.UserConstraints[0].RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = requiredMorning,
                ShiftLabel = ShiftLabel.Morning
            });

            // راه‌حل خالی + یک انتساب غیرمجاز روی روز مرخصی
            var solution = new ShiftSolution();
            solution.AddAssignment(1, 1, leaveDate, ShiftLabel.Morning, isOnCall: true);
            solution.AddAssignment(2, 1, leaveDate, ShiftLabel.Morning, isOnCall: false);

            ApprovedRequestGuard.ForceApply(solution, constraints);

            Assert.False(solution.GetUserAssignments(1, leaveDate).Any());
            Assert.True(solution.GetShiftAssignments(1, requiredMorning).Any(a => a.UserId == 1 && !a.IsOnCall));
            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }

        [Fact]
        public void ApplyMandatoryConstraints_KeepsFullDayPresence_WhenPreviousNightWouldBlockMorning()
        {
            var day0 = new DateTime(2026, 8, 3);
            var day1 = new DateTime(2026, 8, 4);
            var specialty = new SpecialtyRequirement
            {
                SpecialtyId = 10,
                RequiredTotalCount = 1
            };

            var constraints = BuildConstraints(
                start: day0,
                days: 2,
                users: new[]
                {
                    User(1, UserGender.Female),
                    User(2, UserGender.Male),
                    User(3, UserGender.Male),
                },
                specialty: specialty);

            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 2,
                ShiftLabel = ShiftLabel.Evening,
                DepartmentId = 1,
                DurationHours = 8,
                SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
            });
            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 3,
                ShiftLabel = ShiftLabel.Night,
                DepartmentId = 1,
                DurationHours = 8,
                SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
            });
            constraints.GlobalConstraints.MaxShiftsPerDay = 2;
            constraints.UserConstraints[0].UserName = "پریسا جعفری سگوند";
            constraints.UserConstraints[0].RequiredPresenceDates.Add(day1);

            var solution = new ShiftSolution();
            // شب روز قبل → صبح روز حضور را مسدود می‌کند مگر گارد درست عمل کند
            solution.AddAssignment(1, 3, day0, ShiftLabel.Night, isOnCall: false);
            solution.AddAssignment(2, 1, day0, ShiftLabel.Morning, isOnCall: false);
            solution.AddAssignment(2, 1, day1, ShiftLabel.Morning, isOnCall: false);

            var scheduler = new SimulatedAnnealingScheduler(constraints, FastParameters);
            scheduler.ApplyMandatoryConstraints(solution);

            Assert.True(
                solution.GetUserAssignments(1, day1).Any(a => !a.IsOnCall),
                "حضور اجباری کل‌روز باید بعد از گارد توالی همچنان برقرار بماند");
            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }

        [Fact]
        public void ForceApply_HonorsRequiredMorning_WhenPreviousNightBlocksAdjacency()
        {
            var prev = new DateTime(2026, 8, 22);
            var day = new DateTime(2026, 8, 23);
            var specialty = new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 2 };
            var constraints = BuildConstraints(
                start: prev,
                days: 2,
                users: new[] { User(14, UserGender.Female), User(2, UserGender.Female), User(3, UserGender.Female) },
                specialty: specialty);

            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 2,
                ShiftLabel = ShiftLabel.Evening,
                DepartmentId = 1,
                DurationHours = 12,
                ManagerRequiredCount = 2,
                ManagerMinLevel1Count = 1,
                SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
            });
            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 3,
                ShiftLabel = ShiftLabel.Night,
                DepartmentId = 1,
                DurationHours = 12,
                ManagerRequiredCount = 2,
                ManagerMinLevel1Count = 1,
                SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
            });
            constraints.GlobalConstraints.MaxShiftsPerDay = 1;
            constraints.HardRules.EnforceMaxShiftsPerDay = true;
            constraints.HardRules.AllowNightShiftAfterNightShift = false;

            var u14 = constraints.UserConstraints[0];
            u14.UserName = "بهاره بهاری پور";
            u14.ShiftManagerLevel = 1;
            u14.CanBeShiftManager = true;
            u14.RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = day,
                ShiftLabel = ShiftLabel.Morning,
                ShiftId = 1
            });

            var solution = new ShiftSolution();
            solution.AddAssignment(14, 3, prev, ShiftLabel.Night, false);
            solution.AddAssignment(2, 1, day, ShiftLabel.Morning, false);
            solution.AddAssignment(3, 1, day, ShiftLabel.Morning, false);

            ApprovedRequestGuard.ForceApply(solution, constraints);

            Assert.True(solution.GetShiftAssignments(1, day).Any(a => a.UserId == 14 && !a.IsOnCall));
            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }

        [Fact]
        public void ApprovedRequestGuard_ForceApply_UsesShiftId_WhenFrontendSentShiftIdAsLabel()
        {
            // شبیه‌سازی باگ واقعی: فرانت Shift.Id=1/2/3 را به‌جای Label=0/1/2 می‌فرستد
            var day = new DateTime(2026, 8, 27);
            var constraints = new ShiftConstraints
            {
                DepartmentId = 1,
                StartDate = day,
                EndDate = day,
                UserConstraints = new List<UserConstraint>
                {
                    User(1, UserGender.Male),
                    User(2, UserGender.Male),
                    User(3, UserGender.Female),
                },
                ShiftRequirements = new List<ShiftRequirement>
                {
                    new()
                    {
                        ShiftId = 1,
                        ShiftLabel = ShiftLabel.Morning,
                        DepartmentId = 1,
                        DurationHours = 6,
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
                        DurationHours = 6,
                        SpecialtyRequirements = new List<SpecialtyRequirement>
                        {
                            new() { SpecialtyId = 10, RequiredTotalCount = 1 }
                        }
                    },
                    new()
                    {
                        ShiftId = 3,
                        ShiftLabel = ShiftLabel.Night,
                        DepartmentId = 1,
                        DurationHours = 12,
                        SpecialtyRequirements = new List<SpecialtyRequirement>
                        {
                            new() { SpecialtyId = 10, RequiredTotalCount = 1 }
                        }
                    }
                },
                HardRules = new HardRuleSet { ForbidDuplicateDailyAssignments = true, EnforceSpecialtyCapacity = true },
                GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
            };

            // raw Label=1 meant ShiftId=1 (Morning)، نه Evening
            constraints.UserConstraints[0].RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = day,
                ShiftLabel = ShiftLabel.Morning,
                ShiftId = 1
            });

            var solution = new ShiftSolution();
            // پر کردن اشتباه عصر
            solution.AddAssignment(2, 2, day, ShiftLabel.Evening, isOnCall: false);

            ApprovedRequestGuard.ForceApply(solution, constraints);

            Assert.True(solution.GetShiftAssignments(1, day).Any(a => a.UserId == 1 && !a.IsOnCall));
            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }

        /// <summary>
        /// رگرسیون مرداد/مهدی رضایی: عصرهای اجباری نباید بعد از سهمیه شب / عدالت تعطیل / توالی حذف شوند.
        /// </summary>
        [Fact]
        public void ApplyMandatoryConstraints_PreservesRequiredEvenings_AgainstNightQuotaAndHolidayFairness()
        {
            var start = new DateTime(2026, 7, 23); // 1405/05/01
            var eveningA = new DateTime(2026, 7, 25); // 1405/05/03
            var eveningB = new DateTime(2026, 8, 22); // 1405/05/31
            var holidays = new HashSet<DateTime> { eveningA, eveningB };

            var mehdi = User(3, UserGender.Male);
            mehdi.UserName = "مهدی رضایی";
            mehdi.ExactNightShiftCount = 5;
            mehdi.ExactHolidayWeekendNightShiftCount = 1;
            mehdi.MinDaysBetweenNightShifts = 1;
            mehdi.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = eveningA, ShiftLabel = ShiftLabel.Evening });
            mehdi.RequiredShiftSlots.Add(new ShiftSlotConstraint { Date = eveningB, ShiftLabel = ShiftLabel.Evening });

            var peers = new[]
            {
                mehdi,
                User(1, UserGender.Female),
                User(2, UserGender.Male),
                User(4, UserGender.Female),
                User(5, UserGender.Male),
                User(6, UserGender.Female),
            };
            foreach (var p in peers.Skip(1))
            {
                p.ExactNightShiftCount = 4;
                p.ExactHolidayWeekendNightShiftCount = 1;
                p.MinDaysBetweenNightShifts = 1;
            }

            var specialty = new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 2 };
            var constraints = BuildConstraints(start, days: 31, users: peers, specialty: specialty);
            constraints.HolidayDates = holidays;
            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 2,
                ShiftLabel = ShiftLabel.Evening,
                DepartmentId = 1,
                DurationHours = 8,
                SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
            });
            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 3,
                ShiftLabel = ShiftLabel.Night,
                DepartmentId = 1,
                DurationHours = 12,
                SpecialtyRequirements = new List<SpecialtyRequirement> { CloneSpecialty(specialty) }
            });

            // راه‌حل اولیه: عصرهای مهدی هست، ولی عدالت تعطیل / سهمیه شب سعی می‌کنند بدزدند
            var solution = new ShiftSolution();
            solution.AddAssignment(3, 2, eveningA, ShiftLabel.Evening, isOnCall: false);
            solution.AddAssignment(3, 2, eveningB, ShiftLabel.Evening, isOnCall: false);
            // شب سهمیه‌ای روی همان روز عصر (تداخل E+N) — باید به نفع عصر اجباری حذف شود
            solution.AddAssignment(3, 3, eveningA, ShiftLabel.Night, isOnCall: false);
            // عصر تعطیل دیگران برای تحریک HolidayFairness
            solution.AddAssignment(1, 2, eveningA, ShiftLabel.Evening, isOnCall: false);
            solution.AddAssignment(1, 2, eveningB, ShiftLabel.Evening, isOnCall: false);
            solution.AddAssignment(2, 2, eveningA, ShiftLabel.Evening, isOnCall: false);

            var scheduler = new SimulatedAnnealingScheduler(constraints, FastParameters);
            scheduler.ApplyMandatoryConstraints(solution);

            Assert.True(
                solution.GetShiftAssignments(2, eveningA).Any(a => a.UserId == 3 && !a.IsOnCall),
                "required Evening on 1405/05/03 must survive ApplyMandatoryConstraints");
            Assert.True(
                solution.GetShiftAssignments(2, eveningB).Any(a => a.UserId == 3 && !a.IsOnCall),
                "required Evening on 1405/05/31 must survive ApplyMandatoryConstraints");
            Assert.False(
                solution.GetShiftAssignments(3, eveningA).Any(a => a.UserId == 3 && !a.IsOnCall),
                "quota Night must yield to required Evening on the same day");
            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }

        [Fact]
        public void ApplyMandatoryConstraints_PreservesRequiredNight_AgainstProductivityMorningNextDay()
        {
            var nightDate = new DateTime(2026, 8, 26);
            var nextDay = nightDate.AddDays(1);
            var user = User(24, UserGender.Female);
            user.UserName = "مریم امیدی منش";
            user.ExactNightShiftCount = 7;
            user.ProductivityRequiredHours = 152;
            user.IncludedInProductivityPlan = true;
            user.RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = nightDate,
                ShiftLabel = ShiftLabel.Night,
                ShiftId = 3
            });

            var specialty = new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 2 };
            var constraints = BuildConstraints(nightDate.AddDays(-5), days: 15, users: [user, User(2, UserGender.Male), User(3, UserGender.Female)], specialty: specialty);
            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 2,
                ShiftLabel = ShiftLabel.Evening,
                DepartmentId = 1,
                DurationHours = 6,
                SpecialtyRequirements = [CloneSpecialty(specialty)]
            });
            constraints.ShiftRequirements.Add(new ShiftRequirement
            {
                ShiftId = 3,
                ShiftLabel = ShiftLabel.Night,
                DepartmentId = 1,
                DurationHours = 12,
                SpecialtyRequirements = [CloneSpecialty(specialty)]
            });
            constraints.HardRules.EnforceProductivityHours = true;
            constraints.HardRules.AllowEveningAfterNightShift = false;

            var solution = new ShiftSolution();
            foreach (var dayOffset in new[] { 0, 4, 8, 12, 16, 20 })
            {
                var d = nightDate.AddDays(dayOffset - 10);
                if (d < constraints.StartDate || d > constraints.EndDate)
                {
                    continue;
                }

                solution.AddAssignment(24, 3, d, ShiftLabel.Night, false);
                solution.AddAssignment(24, 1, d.AddDays(1), ShiftLabel.Morning, false);
            }

            var scheduler = new SimulatedAnnealingScheduler(constraints, FastParameters);
            scheduler.ApplyMandatoryConstraints(solution);

            Assert.True(
                solution.GetShiftAssignments(3, nightDate).Any(a => a.UserId == 24 && !a.IsOnCall),
                "Required night on 2026-08-26 must survive ApplyMandatoryConstraints");
            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }

        private static SpecialtyRequirement CloneSpecialty(SpecialtyRequirement s) => new()
        {
            SpecialtyId = s.SpecialtyId,
            SpecialtyName = s.SpecialtyName,
            RequiredMaleCount = s.RequiredMaleCount,
            RequiredFemaleCount = s.RequiredFemaleCount,
            RequiredTotalCount = s.RequiredTotalCount,
            OnCallMaleCount = s.OnCallMaleCount,
            OnCallFemaleCount = s.OnCallFemaleCount,
            OnCallTotalCount = s.OnCallTotalCount
        };

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
        ShiftManagerLevel = canBeShiftManager ? ShiftManagerRules.Level1 : null,
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
