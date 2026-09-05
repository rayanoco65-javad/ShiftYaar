using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ShiftYar.Application.Common.Utilities;
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
        RunAndAssert(constraints);
    }

    [Fact]
    public void Test_Dept2_WithRealDbRequests()
    {
        var constraints = BuildDept2ConstraintsWithRealRequests();
        RunAndAssert(constraints);
    }

    private void RunAndAssert(ShiftConstraints constraints)
    {
        Trace.Listeners.Clear();
        Trace.Listeners.Add(new DefaultTraceListener());
        Trace.Listeners.Add(new ConsoleTraceListener());
        var nightReq = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Night);
        var totalQuota = constraints.UserConstraints.Sum(u => u.ExactNightShiftCount ?? 0);
        _output.WriteLine($"Total exact night quota requested: {totalQuota}");

        ShiftSolution solution = null;
        SimulatedAnnealingScheduler scheduler = null;
        for (int run = 1; run <= 3; run++)
        {
            _output.WriteLine($"=== RUN {run} ===");
            scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
            {
                MaxIterations = 4000,
                MaxIterationsWithoutImprovement = 600
            });

            var sw = Stopwatch.StartNew();
            solution = scheduler.Optimize();
            _output.WriteLine($"Optimize run {run} completed in {sw.ElapsedMilliseconds}ms");

            var totNights = solution.Assignments.Values.Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
            _output.WriteLine($"Run {run}: Total assigned nights in solution = {totNights} / 124");
            foreach (var u in constraints.UserConstraints.Where(u => u.HasExactNightQuota).OrderBy(u => u.UserId))
            {
                var cnt = solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                _output.WriteLine($"  User {u.UserId} ({u.UserName}): {cnt} / {u.ExactNightShiftCount}");
            }

            var ok = scheduler.AreExactNightQuotasSatisfied(solution, out var unmetErrors);
            if (!ok)
            {
                _output.WriteLine($"FAILED ON RUN {run}: {string.Join(" | ", unmetErrors)}");
            }
            Assert.True(ok, $"Run {run} failed: " + string.Join("\n", unmetErrors));
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
            var onDate = allNights.Where(a => a.Date.Date == d.Date).Select(a => $"{a.UserId}(L={constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.ShiftManagerLevel})").ToList();
            _output.WriteLine($"  -> Date {d:MM/dd} has {onDate.Count} nights: [{string.Join(", ", onDate)}]");
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
                MinimumShiftsRequired = { [ShiftLabel.Night] = exactNight },
                MaxConsecutiveShifts = 4
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

        return CreateConstraintsObj(startDate, endDate, users);
    }

    private static ShiftConstraints BuildDept2ConstraintsWithRealRequests()
    {
        var startDate = new DateTime(2026, 8, 23);
        var endDate = new DateTime(2026, 9, 22);

        var users = new List<UserConstraint>();

        void AddUser(int id, string name, int exactNight, bool canManage = false, int? level = null)
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
                MinimumShiftsRequired = { [ShiftLabel.Night] = exactNight },
                MaxConsecutiveShifts = 3
            };
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
        AddUser(25, "شکیبا موسیوند", 7, true, 2);
        AddUser(26, "کیمیا کاظمی", 9, true, 2);
        AddUser(27, "فاطمه دبستانیان", 8);
        AddUser(28, "نازنین سبزواری", 8);
        AddUser(29, "گلنوش باقری", 8);
        AddUser(30, "سپیده دریکوند", 9);
        AddUser(31, "فاطمه یوسفی کشکولی", 9);
        AddUser(32, "الناز رستمی چگنی", 8);

        var rawRequests = new (int UserId, string Date, int Type, int? Label, int Action)[]
        {
            (13, "2026-08-23", 0, null, 1),
            (13, "2026-08-24", 0, null, 1),
            (13, "2026-08-25", 0, null, 1),
            (13, "2026-08-26", 0, null, 1),
            (13, "2026-08-27", 0, null, 1),
            (13, "2026-08-28", 1, 1, 1),
            (13, "2026-08-29", 1, 0, 0),
            (13, "2026-09-11", 1, 1, 1),
            (13, "2026-09-12", 1, 0, 0),
            (13, "2026-09-16", 0, null, 1),
            (13, "2026-09-17", 0, null, 1),
            (13, "2026-09-18", 0, null, 1),
            (13, "2026-09-19", 0, null, 1),
            (13, "2026-09-20", 0, null, 1),
            (13, "2026-09-21", 0, null, 1),
            (13, "2026-09-22", 0, null, 1),
            (14, "2026-08-23", 1, 0, 0),
            (14, "2026-08-24", 1, 2, 0),
            (14, "2026-08-27", 1, 0, 0),
            (14, "2026-08-29", 1, 0, 0),
            (14, "2026-08-30", 1, 0, 0),
            (14, "2026-08-31", 1, 2, 0),
            (14, "2026-09-03", 1, 0, 0),
            (14, "2026-09-04", 1, 0, 0),
            (14, "2026-09-05", 1, 0, 0),
            (14, "2026-09-06", 1, 0, 0),
            (14, "2026-09-10", 1, 0, 0),
            (14, "2026-09-12", 1, 0, 0),
            (14, "2026-09-13", 1, 0, 0),
            (14, "2026-09-14", 1, 2, 0),
            (14, "2026-09-17", 1, 0, 0),
            (14, "2026-09-18", 1, 0, 0),
            (14, "2026-09-19", 1, 0, 0),
            (14, "2026-09-20", 1, 0, 0),
            (14, "2026-09-21", 1, 2, 0),
            (15, "2026-08-28", 0, null, 1),
            (15, "2026-09-04", 0, null, 1),
            (15, "2026-09-18", 0, null, 1),
            (15, "2026-09-21", 0, null, 1),
            (15, "2026-09-22", 0, null, 1),
            (16, "2026-08-23", 0, null, 1),
            (16, "2026-09-08", 0, null, 1),
            (16, "2026-09-09", 0, null, 1),
            (17, "2026-08-24", 1, 2, 0),
            (17, "2026-08-31", 1, 2, 0),
            (17, "2026-09-05", 1, 0, 0),
            (17, "2026-09-06", 0, null, 1),
            (17, "2026-09-07", 0, null, 1),
            (17, "2026-09-08", 0, null, 1),
            (17, "2026-09-12", 1, 2, 0),
            (17, "2026-09-19", 1, 2, 0),
            (18, "2026-08-23", 1, 1, 1),
            (18, "2026-08-23", 1, 1, 1),
            (18, "2026-08-23", 1, 2, 1),
            (18, "2026-08-25", 1, 2, 1),
            (18, "2026-08-25", 1, 1, 1),
            (18, "2026-08-30", 1, 1, 1),
            (18, "2026-09-01", 1, 1, 1),
            (18, "2026-09-01", 1, 2, 1),
            (18, "2026-09-06", 1, 2, 1),
            (18, "2026-09-06", 1, 1, 1),
            (18, "2026-09-08", 0, null, 1),
            (18, "2026-09-08", 1, 2, 1),
            (18, "2026-09-09", 0, null, 1),
            (18, "2026-09-13", 1, 1, 1),
            (18, "2026-09-13", 1, 2, 1),
            (18, "2026-09-15", 1, 2, 1),
            (18, "2026-09-15", 1, 1, 1),
            (18, "2026-09-20", 1, 1, 1),
            (18, "2026-09-20", 1, 2, 1),
            (18, "2026-09-21", 0, null, 1),
            (18, "2026-09-22", 0, null, 1),
            (18, "2026-09-22", 1, 1, 1),
            (18, "2026-09-22", 1, 2, 1),
            (19, "2026-08-27", 1, 0, 0),
            (19, "2026-08-30", 1, 0, 0),
            (19, "2026-08-31", 0, null, 1),
            (19, "2026-09-01", 0, null, 1),
            (19, "2026-09-02", 0, null, 1),
            (19, "2026-09-03", 0, null, 1),
            (19, "2026-09-04", 0, null, 1),
            (19, "2026-09-05", 0, null, 1),
            (20, "2026-08-23", 0, null, 1),
            (20, "2026-08-24", 0, null, 1),
            (20, "2026-08-25", 0, null, 1),
            (20, "2026-08-26", 1, 2, 0),
            (20, "2026-08-27", 1, 2, 0),
            (20, "2026-08-28", 1, 1, 0),
            (20, "2026-08-29", 0, null, 1),
            (20, "2026-08-30", 0, null, 1),
            (20, "2026-08-31", 0, null, 1),
            (20, "2026-09-01", 0, null, 1),
            (20, "2026-09-02", 1, 2, 0),
            (20, "2026-09-03", 1, 2, 0),
            (20, "2026-09-04", 1, 1, 0),
            (20, "2026-09-05", 0, null, 1),
            (20, "2026-09-06", 0, null, 1),
            (20, "2026-09-07", 0, null, 1),
            (20, "2026-09-08", 0, null, 1),
            (20, "2026-09-09", 1, 2, 0),
            (20, "2026-09-10", 1, 2, 0),
            (20, "2026-09-11", 1, 1, 0),
            (20, "2026-09-12", 0, null, 1),
            (20, "2026-09-13", 0, null, 1),
            (20, "2026-09-14", 0, null, 1),
            (20, "2026-09-15", 0, null, 1),
            (20, "2026-09-16", 1, 2, 0),
            (20, "2026-09-17", 1, 2, 0),
            (20, "2026-09-18", 1, 1, 0),
            (20, "2026-09-19", 0, null, 1),
            (20, "2026-09-20", 0, null, 1),
            (20, "2026-09-21", 0, null, 1),
            (20, "2026-09-22", 0, null, 1),
            (21, "2026-08-23", 0, null, 1),
            (21, "2026-08-24", 0, null, 1),
            (22, "2026-08-29", 0, null, 1),
            (22, "2026-08-30", 0, null, 1),
            (22, "2026-08-31", 0, null, 1),
            (22, "2026-09-18", 0, null, 1),
            (22, "2026-09-19", 0, null, 1),
            (22, "2026-09-20", 0, null, 1),
            (22, "2026-09-21", 0, null, 1),
            (24, "2026-08-24", 1, 2, 0),
            (24, "2026-08-26", 1, 2, 0),
            (24, "2026-08-29", 0, null, 1),
            (24, "2026-08-30", 0, null, 1),
            (24, "2026-08-31", 0, null, 1),
            (24, "2026-09-01", 0, null, 1),
            (24, "2026-09-02", 0, null, 1),
            (24, "2026-09-03", 0, null, 1),
            (24, "2026-09-04", 0, null, 1),
            (24, "2026-09-05", 1, 2, 0),
            (24, "2026-09-07", 1, 2, 0),
            (24, "2026-09-09", 1, 2, 0),
            (24, "2026-09-16", 1, 2, 0),
            (24, "2026-09-21", 1, 2, 0),
            (25, "2026-08-24", 0, null, 1),
            (25, "2026-08-26", 0, null, 1),
            (25, "2026-08-29", 1, 1, 1),
            (25, "2026-08-31", 0, null, 1),
            (25, "2026-09-02", 1, 1, 1),
            (25, "2026-09-05", 1, 1, 1),
            (25, "2026-09-07", 1, 1, 1),
            (25, "2026-09-08", 0, null, 1),
            (25, "2026-09-09", 0, null, 1),
            (25, "2026-09-10", 0, null, 1),
            (25, "2026-09-11", 0, null, 1),
            (25, "2026-09-12", 0, null, 1),
            (25, "2026-09-13", 0, null, 1),
            (25, "2026-09-14", 0, null, 1),
            (25, "2026-09-16", 1, 1, 1),
            (25, "2026-09-19", 1, 1, 1),
            (25, "2026-09-21", 1, 1, 1),
            (26, "2026-08-24", 0, null, 1),
            (26, "2026-08-25", 0, null, 1),
            (26, "2026-08-26", 0, null, 1),
            (26, "2026-08-27", 0, null, 1),
            (26, "2026-08-28", 0, null, 1),
            (26, "2026-09-05", 0, null, 1),
            (28, "2026-08-25", 1, 1, 1),
            (28, "2026-08-29", 1, 1, 1),
            (28, "2026-09-01", 1, 1, 1),
            (28, "2026-09-05", 0, null, 1),
            (28, "2026-09-06", 0, null, 1),
            (28, "2026-09-08", 1, 1, 1),
            (28, "2026-09-09", 0, null, 1),
            (28, "2026-09-12", 1, 1, 1),
            (28, "2026-09-15", 1, 1, 1),
            (28, "2026-09-19", 1, 1, 1),
            (28, "2026-09-22", 1, 1, 1),
            (29, "2026-09-04", 0, null, 1),
            (29, "2026-09-05", 0, null, 1),
            (29, "2026-09-06", 0, null, 1),
            (30, "2026-09-19", 0, null, 1),
            (30, "2026-09-20", 0, null, 1),
            (30, "2026-09-21", 0, null, 1),
            (30, "2026-09-22", 0, null, 1),
            (30, "2026-09-22", 0, null, 1),
            (31, "2026-08-23", 0, null, 1),
            (31, "2026-08-24", 0, null, 1),
            (31, "2026-09-16", 0, null, 1),
            (31, "2026-09-17", 0, null, 1),
            (31, "2026-09-18", 0, null, 1),
            (32, "2026-08-26", 0, null, 1),
            (32, "2026-09-04", 0, null, 1),
            (32, "2026-09-05", 0, null, 1),
            (32, "2026-09-06", 0, null, 1),
            (32, "2026-09-07", 0, null, 1),
        };

        foreach (var req in rawRequests)
        {
            var u = users.FirstOrDefault(x => x.UserId == req.UserId);
            if (u == null) continue;
            var date = DateTime.Parse(req.Date);

            if (req.Action == 1) // RequestToBeOffShift
            {
                if (req.Type == 0) // FullDay
                {
                    if (!u.UnavailableDates.Any(d => d.Date == date))
                    {
                        u.UnavailableDates.Add(date);
                        u.UnavailableShiftSlots.Add(new ShiftSlotConstraint
                        {
                            Date = date.AddDays(-1),
                            ShiftLabel = ShiftLabel.Night,
                            ShiftId = 6
                        });
                    }
                }
                else if (req.Type == 1 && req.Label.HasValue) // SpecificShift
                {
                    var lbl = (ShiftLabel)req.Label.Value;
                    var shiftId = lbl == ShiftLabel.Morning ? 4 : lbl == ShiftLabel.Evening ? 5 : 6;
                    u.UnavailableShiftSlots.Add(new ShiftSlotConstraint
                    {
                        Date = date,
                        ShiftLabel = lbl,
                        ShiftId = shiftId
                    });
                    if (lbl == ShiftLabel.Morning)
                    {
                        u.UnavailableShiftSlots.Add(new ShiftSlotConstraint
                        {
                            Date = date.AddDays(-1),
                            ShiftLabel = ShiftLabel.Night,
                            ShiftId = 6
                        });
                    }
                }
            }
            else if (req.Action == 0) // RequestToBeOnShift
            {
                if (req.Type == 1 && req.Label.HasValue) // SpecificShift
                {
                    var lbl = (ShiftLabel)req.Label.Value;
                    var shiftId = lbl == ShiftLabel.Morning ? 4 : lbl == ShiftLabel.Evening ? 5 : 6;
                    u.RequiredShiftSlots.Add(new ShiftSlotConstraint
                    {
                        Date = date,
                        ShiftLabel = lbl,
                        ShiftId = shiftId
                    });
                }
            }
        }

        return CreateConstraintsObj(startDate, endDate, users);
    }

    private static ShiftConstraints CreateConstraintsObj(DateTime startDate, DateTime endDate, List<UserConstraint> users)
    {
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
                MaxConsecutiveNightShifts = 2,
                MaxShiftsPerDay = 1
            }
        };
    }
}
