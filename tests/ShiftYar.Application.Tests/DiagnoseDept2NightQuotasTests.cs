using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using Xunit.Abstractions;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class DiagnoseDept2NightQuotasTests
{
    private readonly ITestOutputHelper _output;

    public DiagnoseDept2NightQuotasTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_Dept2_NightQuotas_AllUsersSatisfied()
    {
        var constraints = BuildDept2Constraints();

        var nightReq = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Night);
        var totalQuota = constraints.UserConstraints.Sum(u => u.ExactNightShiftCount ?? 0);
        _output.WriteLine($"Total exact night quota requested: {totalQuota}");
        Assert.Equal(124, totalQuota);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 3000,
            MaxIterationsWithoutImprovement = 400
        });

        var sw = Stopwatch.StartNew();
        ShiftSolution solution;
        try
        {
            solution = scheduler.Optimize();
            _output.WriteLine($"Optimize completed in {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            _output.WriteLine("EXCEPTION CAUGHT: " + ex.Message);
            throw;
        }

        var unmet = new List<string>();
        foreach (var u in constraints.UserConstraints.Where(u => u.HasExactNightQuota).OrderBy(u => u.UserId))
        {
            var userNights = solution.GetUserAllAssignments(u.UserId)
                .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                .OrderBy(a => a.Date)
                .ToList();

            var datesStr = string.Join(", ", userNights.Select(n => n.Date.ToString("MM/dd")));
            _output.WriteLine($"User {u.UserId} ({u.UserName}): assigned {userNights.Count} of {u.ExactNightShiftCount} nights: [{datesStr}]");

            if (userNights.Count < u.ExactNightShiftCount!.Value)
            {
                unmet.Add($"User {u.UserId} ({u.UserName}): {userNights.Count}/{u.ExactNightShiftCount.Value}");
                var allUserAssignments = solution.GetUserAllAssignments(u.UserId).OrderBy(a => a.Date).ThenBy(a => a.ShiftLabel);
                _output.WriteLine($"  -> All assignments for User {u.UserId}: {string.Join(", ", allUserAssignments.Select(a => $"{a.Date:MM/dd}:{a.ShiftLabel}"))}");
            }
        }

        // Also check if any donor has surplus
        foreach (var u in constraints.UserConstraints.Where(u => u.HasExactNightQuota).OrderBy(u => u.UserId))
        {
            var userNights = solution.GetUserAllAssignments(u.UserId)
                .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                .ToList();
            if (userNights.Count > u.ExactNightShiftCount!.Value)
            {
                _output.WriteLine($"  -> SURPLUS: User {u.UserId} ({u.UserName}): has {userNights.Count} vs exact {u.ExactNightShiftCount.Value}");
            }
        }

        // Check total nights in solution across all dates
        var allNights = solution.Assignments.Values.Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).ToList();
        _output.WriteLine($"Total nights in solution: {allNights.Count} (expected 124)");
        for (var d = constraints.StartDate; d <= constraints.EndDate; d = d.AddDays(1))
        {
            var countOnDate = allNights.Count(a => a.Date.Date == d.Date);
            if (countOnDate != 4)
            {
                _output.WriteLine($"  -> Date {d:MM/dd} has {countOnDate} nights instead of 4!");
            }
        }



        Assert.True(scheduler.AreExactNightQuotasSatisfied(solution, out var quotaErrors),
            "AreExactNightQuotasSatisfied should be true. Errors:\n" + string.Join("\n", quotaErrors));
        Assert.Empty(unmet);
    }

    private static ShiftConstraints BuildDept2Constraints()
    {
        var startDate = new DateTime(2026, 8, 23);
        var endDate = new DateTime(2026, 9, 22);

        var users = new List<UserConstraint>();

        void AddUser(int id, string name, int exactNight, bool canManage = false, int? level = null,
            IEnumerable<(DateTime Date, ShiftLabel Label)> unavailSlots = null,
            IEnumerable<DateTime> unavailDates = null)
        {
            var u = new UserConstraint
            {
                UserId = id,
                UserName = name,
                IsActive = true,
                SpecialtyId = 10,
                CanBeShiftManager = canManage,
                ShiftManagerLevel = (byte?)level,
                ExactNightShiftCount = exactNight,
                NightFallbackParticipation = false,
                MinimumShiftsRequired = { [ShiftLabel.Night] = exactNight }
            };
            if (unavailSlots != null)
            {
                foreach (var (d, l) in unavailSlots)
                {
                    u.UnavailableShiftSlots.Add(new ShiftSlotConstraint { Date = d, ShiftLabel = l });
                }
            }
            if (unavailDates != null)
            {
                foreach (var d in unavailDates)
                {
                    u.UnavailableDates.Add(d);
                }
            }
            users.Add(u);
        }

        AddUser(14, "بهاره بهاری پور", 4, true, 1);
        AddUser(16, "فاطمه سلیمی", 4, true, 1);
        AddUser(17, "فاطمه رضایی", 4, true, 1);
        AddUser(18, "خدیجه متقی", 4, true, 1);
        AddUser(19, "حدیث کاظمی", 5, true, 1);
        AddUser(20, "فاطمه مدهنی", 8, true, 1);
        AddUser(21, "عاطفه رحیمی منفرد", 5, true, 1);
        AddUser(22, "فاطمه رازانی", 8, true, 2);
        AddUser(23, "مریم کرمی", 8, true, 2);
        AddUser(24, "مریم امیدی منش", 8, true, 2);
        AddUser(25, "شکیبا موسیوند", 7);
        AddUser(26, "کیمیا کاظمی", 9, true, 2,
            unavailSlots: new[] {
                (new DateTime(2026, 8, 23), ShiftLabel.Night),
                (new DateTime(2026, 8, 24), ShiftLabel.Night),
                (new DateTime(2026, 8, 25), ShiftLabel.Night),
                (new DateTime(2026, 8, 26), ShiftLabel.Night),
                (new DateTime(2026, 8, 27), ShiftLabel.Night),
                (new DateTime(2026, 9, 4), ShiftLabel.Night)
            },
            unavailDates: new[] {
                new DateTime(2026, 8, 24),
                new DateTime(2026, 8, 25),
                new DateTime(2026, 8, 26),
                new DateTime(2026, 8, 27),
                new DateTime(2026, 8, 28),
                new DateTime(2026, 9, 5)
            });
        AddUser(27, "فاطمه دبستانیان", 8);
        AddUser(28, "نازنین سبزواری", 8);
        AddUser(29, "گلنوش باقری", 8);
        AddUser(30, "سپیده دریکوند", 9, false, null,
            unavailSlots: new[] {
                (new DateTime(2026, 9, 18), ShiftLabel.Night),
                (new DateTime(2026, 9, 19), ShiftLabel.Night),
                (new DateTime(2026, 9, 20), ShiftLabel.Night),
                (new DateTime(2026, 9, 21), ShiftLabel.Night)
            },
            unavailDates: new[] {
                new DateTime(2026, 9, 19),
                new DateTime(2026, 9, 20),
                new DateTime(2026, 9, 21),
                new DateTime(2026, 9, 22)
            });
        AddUser(31, "فاطمه یوسفی کشکولی", 9);
        AddUser(32, "الناز رستمی چگنی", 8);

        return new ShiftConstraints
        {
            DepartmentId = 2,
            StartDate = startDate,
            EndDate = endDate,
            UserConstraints = users,
            ShiftRequirements = new List<ShiftRequirement>
            {
                new()
                {
                    ShiftId = 4,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 2,
                    DurationHours = 6,
                    SpecialtyRequirements = { new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 4 } }
                },
                new()
                {
                    ShiftId = 5,
                    ShiftLabel = ShiftLabel.Evening,
                    DepartmentId = 2,
                    DurationHours = 6,
                    ManagerRequiredCount = 2,
                    ManagerMinLevel1Count = 0,
                    SpecialtyRequirements = { new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 4 } }
                },
                new()
                {
                    ShiftId = 6,
                    ShiftLabel = ShiftLabel.Night,
                    DepartmentId = 2,
                    DurationHours = 12,
                    ManagerRequiredCount = 2,
                    ManagerMinLevel1Count = 1,
                    SpecialtyRequirements = { new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 4 } }
                }
            },
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMaxConsecutiveShifts = true,
                AllowNightShiftAfterNightShift = false,
                AllowEveningAfterNightShift = false
            },
            GlobalConstraints = new GlobalConstraints
            {
                MaxShiftsPerDay = 2
            }
        };
    }
}
