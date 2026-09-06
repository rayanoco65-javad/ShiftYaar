using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.Features.ShiftModel.Jobs;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Application.Interfaces.ShiftModel;
using ShiftYar.Infrastructure;
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
    public void Test_AnalyzeShiftDistribution()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var solution = scheduler.Optimize();

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution);
        if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
        {
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
        }
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);

        _output.WriteLine("========================================================================================================================");
        _output.WriteLine(string.Format("{0,-3} | {1,-20} | {2,-6} | {3,-4} {4,-4} {5,-4} | {6,-4} {7,-4} {8,-4} = {9,-4} | {10,-11} | {11,-6} {12,-6} {13,-6} | {14}",
            "ID", "Name", "Exp", "ReqM", "ReqE", "ReqN", "AssM", "AssE", "AssN", "Tot", "Holidays", "Worked", "ReqHrs", "Diff", "ExtraShiftsBeyondReqs"));
        _output.WriteLine("========================================================================================================================");

        foreach (var u in constraints.UserConstraints.OrderBy(u => u.UserId))
        {
            var asgs = solution.GetUserAllAssignments(u.UserId).Where(a => !a.IsOnCall).ToList();
            var assM = asgs.Count(a => a.ShiftLabel == ShiftLabel.Morning);
            var assE = asgs.Count(a => a.ShiftLabel == ShiftLabel.Evening);
            var assN = asgs.Count(a => a.ShiftLabel == ShiftLabel.Night);
            var tot = asgs.Count;

            var reqM = u.RequiredShiftSlots.Count(r => r.ShiftLabel == ShiftLabel.Morning);
            var reqE = u.RequiredShiftSlots.Count(r => r.ShiftLabel == ShiftLabel.Evening);
            var reqN = u.RequiredShiftSlots.Count(r => r.ShiftLabel == ShiftLabel.Night);
            var totReq = u.RequiredShiftSlots.Count;

            // Extra discretionary shifts (assigned shifts on dates/labels that were NOT in RequiredShiftSlots)
            var extraShifts = asgs.Count(a => !u.RequiredShiftSlots.Any(r => r.ShiftLabel == a.ShiftLabel && r.Date.Date == a.Date.Date));

            // Calculate worked hours using scheduler reflection or formula
            var calcMethod = typeof(SimulatedAnnealingScheduler).GetMethod("CalculateUserWorkedHours", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var workedHours = (double)calcMethod!.Invoke(scheduler, new object[] { asgs })!;
            var reqHours = (double)(u.ProductivityRequiredHours ?? 0m);
            var diff = workedHours - reqHours;

            var holM = asgs.Count(a => constraints.HolidayDates.Any(h => h.Date == a.Date.Date) && a.ShiftLabel == ShiftLabel.Morning);
            var holE = asgs.Count(a => constraints.HolidayDates.Any(h => h.Date == a.Date.Date) && a.ShiftLabel == ShiftLabel.Evening);

            _output.WriteLine(string.Format("{0,-3} | {1,-20} | Exp={2,-2} | {3,-4} {4,-4} {5,-4} | {6,-4} {7,-4} {8,-4} = {9,-4} | HolM={10} HolE={11} | {12,6:F1} {13,6:F1} {14,6:F1} | Extra={15}",
                u.UserId, u.UserName, u.ExperienceYears, reqM, reqE, reqN, assM, assE, assN, tot, holM, holE, workedHours, reqHours, diff, extraShifts));
        }
        _output.WriteLine("========================================================================================================================");

        foreach (var targetDate in new[] { new DateTime(2026, 9, 20), new DateTime(2026, 9, 21), new DateTime(2026, 9, 22) })
        {
            var dayAsgs = solution.Assignments.Values.Where(a => a.Date.Date == targetDate.Date && !a.IsOnCall).OrderBy(a => a.ShiftLabel).ThenBy(a => a.UserId).ToList();
            _output.WriteLine($"Date {targetDate:yyyy-MM-dd}: Total={dayAsgs.Count} (Morning={dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Morning)}, Evening={dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Evening)}, Night={dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Night)})");
            foreach (var a in dayAsgs)
            {
                var u = constraints.UserConstraints.First(x => x.UserId == a.UserId);
                _output.WriteLine($"   {a.ShiftLabel}: User {a.UserId} ({u.UserName})");
            }
        }
        _output.WriteLine("========================================================================================================================");
    }

    [Fact]
    public void Test_FromSavedConstraintsFile()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json);

        _output.WriteLine($"Loaded constraints from file: {constraints.UserConstraints.Count} users");

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var solution = scheduler.Optimize();

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution);
        if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
        {
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
        }
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);

        var nightShift = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Night);
        var u14 = constraints.UserConstraints.First(u => u.UserId == 14);
        var u26 = constraints.UserConstraints.First(u => u.UserId == 26);

        var u14Nights = solution.GetUserAllAssignments(14).Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).OrderBy(a => a.Date).ToList();
        _output.WriteLine($"=== USER 14 NIGHTS ({u14Nights.Count}/4) ===");
        foreach (var n in u14Nights)
        {
            var others = solution.Assignments.Values.Where(a => a.Date.Date == n.Date.Date && a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).ToList();
            var details = string.Join(", ", others.Select(o => $"{o.UserId}(L={constraints.UserConstraints.First(u => u.UserId == o.UserId).ShiftManagerLevel})"));
            var isReq = u14.RequiredShiftSlots.Any(r => r.Date.Date == n.Date.Date && r.ShiftLabel == ShiftLabel.Night);
            _output.WriteLine($"  {n.Date:yyyy-MM-dd} (IsRequired={isReq}): [{details}]");
        }

        var u26Nights = solution.GetUserAllAssignments(26).Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).OrderBy(a => a.Date).ToList();
        _output.WriteLine($"=== KIMIA (26) NIGHTS ({u26Nights.Count}/9) ===");
        foreach (var n in u26Nights)
        {
            _output.WriteLine($"  {n.Date:yyyy-MM-dd}");
        }

        _output.WriteLine("=== ALL 31 NIGHTS MANAGER MIX & ASSIGNMENTS ===");
        for (var d = constraints.StartDate.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
        {
            var asgs = solution.Assignments.Values.Where(a => a.Date.Date == d.Date && a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).ToList();
            var l1Count = asgs.Count(a => constraints.UserConstraints.First(u => u.UserId == a.UserId).ShiftManagerLevel == 1);
            var l2Count = asgs.Count(a => constraints.UserConstraints.First(u => u.UserId == a.UserId).ShiftManagerLevel == 2);
            var details = string.Join(", ", asgs.Select(o => $"{o.UserId}(L={constraints.UserConstraints.First(u => u.UserId == o.UserId).ShiftManagerLevel})"));
            _output.WriteLine($"  {d:yyyy-MM-dd}: L1={l1Count}, L2={l2Count}, Total={asgs.Count} | [{details}]");
        }

        Assert.True(scheduler.AreExactNightQuotasSatisfied(solution, out var quotaErrors), string.Join("; ", quotaErrors));
    }

    [Fact]
    public async Task Test_LiaraDb_DirectScheduling()
    {
        var connStr = "Data Source=chogolisa.liara.cloud,34729;Initial Catalog=ShiftYarDb2;User Id=sa;Password=DwOr8efLcjXBVQ10jGYx5dhy;MultipleActiveResultSets=true;TrustServerCertificate=true";
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = connStr
        }).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(builder => builder.AddConsole());
        services.AddInfrastructure(config);
        services.AddApplication();
        services.AddHttpContextAccessor();
        services.AddSingleton<ISchedulingJobStore, SchedulingJobStore>();
        var sp = services.BuildServiceProvider();
        var service = (ShiftYar.Application.Features.ShiftModel.Services.ShiftSchedulingService)sp.GetRequiredService<IShiftSchedulingService>();

        var req = new ShiftSchedulingRequestDto
        {
            DepartmentId = 2,
            StartDate = "1405/06/01",
            EndDate = "1405/06/31",
            Algorithm = SchedulingAlgorithm.SimulatedAnnealing
        };

        _output.WriteLine("Calling LoadConstraintsAsync...");
        var constraints = await service.LoadConstraintsAsync(req);
        _output.WriteLine($"Loaded {constraints.UserConstraints.Count} users, {constraints.ShiftRequirements.Count} shift requirements.");

        var json = System.Text.Json.JsonSerializer.Serialize(constraints, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(@"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json", json);
        _output.WriteLine("Saved constraints to scratch\\dept2_loaded_constraints.json");

        ExactNightQuotaGuard.LogAction = msg => _output.WriteLine("[ExactNightQuotaGuard] " + msg);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var sw = Stopwatch.StartNew();
        var solution = scheduler.Optimize();
        _output.WriteLine($"Optimize completed in {sw.ElapsedMilliseconds}ms");

        void PrintNightCounts(string stage)
        {
            _output.WriteLine($"=== NIGHT COUNTS: {stage} ===");
            var tot = solution.Assignments.Values.Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
            _output.WriteLine($"Total nights = {tot} / 124");
            foreach (var u in constraints.UserConstraints.OrderBy(u => u.UserId))
            {
                var cnt = solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                if (u.HasExactNightQuota || cnt > 0)
                {
                    _output.WriteLine($"  User {u.UserId} ({u.UserName}, L={u.ShiftManagerLevel}, SubType={u.ShiftSubType}): {cnt} / {u.ExactNightShiftCount?.ToString() ?? "NO_QUOTA"}");
                }
            }
        }

        PrintNightCounts("After Optimize");

        _output.WriteLine("Running ForceSatisfyAllDeficits...");
        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        PrintNightCounts("After ForceSatisfyAllDeficits 1");

        _output.WriteLine("Running GlobalRebalanceNightQuotas...");
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        PrintNightCounts("After GlobalRebalanceNightQuotas 1");

        _output.WriteLine("Running Enforce...");
        ExactNightQuotaGuard.Enforce(solution, constraints);
        PrintNightCounts("After Enforce 1");

        _output.WriteLine("Running PerformFinalManagerMixRepairSweep...");
        scheduler.PerformFinalManagerMixRepairSweep(solution);
        PrintNightCounts("After ManagerMixSweep");

        if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
        {
            _output.WriteLine("Re-running repairs...");
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
            PrintNightCounts("After Second Repair");
        }

        _output.WriteLine("Running StripExcessCoverage...");
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        PrintNightCounts("After StripExcessCoverage");

        var kimia = constraints.UserConstraints.FirstOrDefault(u => u.UserId == 26);
        if (kimia != null)
        {
            var kimiaNights = solution.GetUserAllAssignments(26).Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).OrderBy(a => a.Date).ToList();
            _output.WriteLine($"Kimia nights count = {kimiaNights.Count} / {kimia.ExactNightShiftCount}:");
            foreach (var kn in kimiaNights)
            {
                _output.WriteLine($"  {kn.Date:yyyy-MM-dd}");
            }
            _output.WriteLine("Kimia unavailable slots:");
            foreach (var un in kimia.UnavailableShiftSlots)
            {
                _output.WriteLine($"  {un.Date:yyyy-MM-dd} {un.ShiftLabel}");
            }
            _output.WriteLine("Kimia unavailable dates:");
            foreach (var ud in kimia.UnavailableDates)
            {
                _output.WriteLine($"  {ud:yyyy-MM-dd}");
            }
        }

        var ok = scheduler.AreExactNightQuotasSatisfied(solution, out var quotaErrors);
        Assert.True(ok, "AreExactNightQuotasSatisfied failed: " + string.Join("; ", quotaErrors));
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
        ExactNightQuotaGuard.LogAction = msg => _output.WriteLine(msg);
        var nightReq = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Night);
        var totalQuota = constraints.UserConstraints.Sum(u => u.ExactNightShiftCount ?? 0);
        _output.WriteLine($"Total exact night quota requested: {totalQuota}");

        ShiftSolution solution = null;
        SimulatedAnnealingScheduler scheduler = null;
        for (int run = 1; run <= 1; run++)
        {
            Console.Error.WriteLine($"=== RUN {run} START ===");
            _output.WriteLine($"=== RUN {run} ===");
            scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
            {
                MaxIterations = 4000,
                MaxIterationsWithoutImprovement = 600
            });

            var sw = Stopwatch.StartNew();
            solution = scheduler.Optimize();
            Console.Error.WriteLine($"Optimize run {run} completed in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Optimize run {run} completed in {sw.ElapsedMilliseconds}ms");

            Console.Error.WriteLine("Starting ForceSatisfyAllDeficits...");
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            Console.Error.WriteLine("Starting GlobalRebalanceNightQuotas...");
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            Console.Error.WriteLine("Starting Enforce...");
            ExactNightQuotaGuard.Enforce(solution, constraints);
            Console.Error.WriteLine("Starting PerformFinalManagerMixRepairSweep...");
            scheduler.PerformFinalManagerMixRepairSweep(solution);
            if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
            {
                Console.Error.WriteLine("Re-running repairs...");
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
                ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
                ExactNightQuotaGuard.Enforce(solution, constraints);
            }
            Console.Error.WriteLine("Starting StripExcessCoverage...");
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            Console.Error.WriteLine("Done post-processing!");

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
                _output.WriteLine("=== ASSIGNMENTS FROM 09-02 to 09-06 ===");
                for (var cd = new DateTime(2026, 9, 2); cd <= new DateTime(2026, 9, 6); cd = cd.AddDays(1))
                {
                    var asgs = solution.Assignments.Values.Where(a => a.Date.Date == cd.Date && !a.IsOnCall)
                        .OrderBy(a => a.ShiftLabel).ThenBy(a => a.UserId);
                    _output.WriteLine($"  {cd:yyyy-MM-dd}: " + string.Join(", ", asgs.Select(a => $"{a.UserId}:{a.ShiftLabel}")));
                }
                foreach (var u in constraints.UserConstraints.Where(u => u.HasExactNightQuota))
                {
                    var cnt = solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                    if (cnt < u.ExactNightShiftCount)
                    {
                        _output.WriteLine($"  Deficit User {u.UserId} ({u.UserName}): {cnt}/{u.ExactNightShiftCount}");
                        _output.WriteLine($"    All assignments: {string.Join(", ", solution.GetUserAllAssignments(u.UserId).OrderBy(a => a.Date).Select(a => $"{a.Date:MM-dd}:{a.ShiftLabel}"))}");
                        _output.WriteLine($"    UnavailableDates: {string.Join(", ", u.UnavailableDates.Select(d => d.ToString("MM-dd")))}");
                        _output.WriteLine($"    UnavailableSlots: {string.Join(", ", u.UnavailableShiftSlots.Select(s => $"{s.Date:MM-dd}:{s.ShiftLabel}"))}");
                    }
                }
            }
            Assert.True(ok, $"Run {run} failed: " + string.Join("\n", unmetErrors));

            var runOverCapacity = ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints);
            Assert.Empty(runOverCapacity);
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

        var finalOverCapacity = ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints);
        Assert.Empty(finalOverCapacity);
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

    [Fact]
    public void Test_Dept2ExactNightQuotasAndCoverageSatisfied()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var solution = scheduler.Optimize();

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution);
        if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
        {
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
        }
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);

        var nightShift = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Night);

        _output.WriteLine("=== USER NIGHT QUOTAS ===");
        var deficits = new List<UserConstraint>();
        var surpluses = new List<UserConstraint>();
        foreach (var u in constraints.UserConstraints.Where(u => u.HasExactNightQuota))
        {
            var count = solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
            var diff = count - u.ExactNightShiftCount!.Value;
            _output.WriteLine($"User {u.UserId} ({u.UserName}, L={u.ShiftManagerLevel}): assigned={count}, quota={u.ExactNightShiftCount} (diff={diff:+0;-0;0})");
            if (diff < 0) deficits.Add(u);
            if (diff > 0) surpluses.Add(u);
        }

        _output.WriteLine("\n=== SURPLUS USER NIGHTS ===");
        foreach (var s in surpluses)
        {
            var nights = solution.GetUserAllAssignments(s.UserId).Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).OrderBy(a => a.Date).ToList();
            _output.WriteLine($"Surplus User {s.UserId} ({s.UserName}, L={s.ShiftManagerLevel}):");
            foreach (var n in nights)
            {
                var assignees = solution.GetShiftAssignments(nightShift.ShiftId, n.Date).Where(a => !a.IsOnCall).ToList();
                var l1Count = assignees.Count(a => constraints.UserConstraints.First(u => u.UserId == a.UserId).ShiftManagerLevel == 1);
                var isCrit = ShiftManagerRules.IsCriticalForManagerMix(constraints, solution, n);
                var isReq = s.RequiredShiftSlots.Any(r => r.Date.Date == n.Date.Date && r.ShiftLabel == ShiftLabel.Night);
                _output.WriteLine($"  {n.Date:yyyy-MM-dd}: L1Count={l1Count}, IsCritical={isCrit}, IsRequired={isReq}, Assignees=[{string.Join(", ", assignees.Select(a => $"{a.UserId}(L={constraints.UserConstraints.First(u => u.UserId == a.UserId).ShiftManagerLevel})"))}]");
            }
        }

        _output.WriteLine("\n=== TESTING DIRECT DONATION FROM SURPLUS TO DEFICIT ===");
        foreach (var def in deficits)
        {
            _output.WriteLine($"\nChecking Deficit User {def.UserId} ({def.UserName}, L={def.ShiftManagerLevel}, need {def.ExactNightShiftCount - solution.GetUserAllAssignments(def.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)} nights):");
            var defNights = solution.GetUserAllAssignments(def.UserId).Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).Select(a => a.Date.Date).ToHashSet();

            foreach (var s in surpluses)
            {
                var nights = solution.GetUserAllAssignments(s.UserId).Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).OrderBy(a => a.Date).ToList();
                foreach (var n in nights)
                {
                    var d = n.Date.Date;
                    var reasons = new List<string>();

                    if (def.UnavailableDates.Any(x => x.Date == d)) reasons.Add("UnavailableDate");
                    if (def.UnavailableShiftSlots.Any(x => x.Date.Date == d && x.ShiftLabel == ShiftLabel.Night)) reasons.Add("UnavailableShiftSlot(Night)");
                    if (defNights.Contains(d)) reasons.Add("AlreadyHasNight");
                    if (defNights.Any(existing => Math.Abs((existing - d).Days) <= 1)) reasons.Add($"SpacingViolation (has nights within 1 day: {string.Join(",", defNights.Where(e => Math.Abs((e - d).Days) <= 1).Select(e => e.ToString("MM-dd")))})");

                    var assigneesWithoutDonor = solution.GetShiftAssignments(nightShift.ShiftId, d)
                        .Where(a => !a.IsOnCall && a.UserId != s.UserId)
                        .Select(a => constraints.UserConstraints.First(u => u.UserId == a.UserId))
                        .ToList();
                    assigneesWithoutDonor.Add(def);
                    var (reqTotal, reqL1) = ShiftManagerRules.GetRequirement(nightShift);
                    if (!ShiftManagerRules.IsSatisfied(assigneesWithoutDonor, reqTotal, reqL1))
                    {
                        reasons.Add($"ManagerMixFails (L1={assigneesWithoutDonor.Count(u => u.ShiftManagerLevel == 1)}, Mgr={assigneesWithoutDonor.Count(u => ShiftManagerRules.IsManager(u))})");
                    }

                    _output.WriteLine($"  Can take from User {s.UserId} on {d:yyyy-MM-dd}? {(reasons.Count == 0 ? "YES!" : string.Join(", ", reasons))}");
                }
            }
        }

        _output.WriteLine("\n=== ASSIGNMENTS ON 2026-09-21 (30 Shahrivar) ===");
        var sep21 = new DateTime(2026, 9, 21);
        var asgsSep21 = solution.Assignments.Values.Where(a => a.Date.Date == sep21).OrderBy(a => a.ShiftLabel).ThenBy(a => a.UserId).ToList();
        _output.WriteLine($"Total shifts on 2026-09-21: {asgsSep21.Count} (Morning={asgsSep21.Count(a => a.ShiftLabel == ShiftLabel.Morning)}, Evening={asgsSep21.Count(a => a.ShiftLabel == ShiftLabel.Evening)}, Night={asgsSep21.Count(a => a.ShiftLabel == ShiftLabel.Night)})");
        foreach (var a in asgsSep21)
        {
            var u = constraints.UserConstraints.First(x => x.UserId == a.UserId);
            _output.WriteLine($"  {a.ShiftLabel}: User {a.UserId} ({u.UserName}, L={u.ShiftManagerLevel})");
        }

        var isSatisfied = scheduler.AreExactNightQuotasSatisfied(solution, out var violationMsg);
        Assert.True(isSatisfied, string.Join("; ", violationMsg));
        Assert.Equal(11, asgsSep21.Count);
    }

    [Fact]
    public void Test_CheckOverCapacityViolations()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var solution = scheduler.Optimize();

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution);
        if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
        {
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
        }
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);

        var over = ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints);
        _output.WriteLine("=== OVER CAPACITY VIOLATIONS ===");
        foreach (var v in over)
        {
            _output.WriteLine(v);
        }

        var dates = new[] { new DateTime(2026, 8, 28), new DateTime(2026, 9, 11), new DateTime(2026, 9, 18) };
        foreach (var d in dates)
        {
            _output.WriteLine($"\n=== Assignments on {d:yyyy-MM-dd} (IsHoliday={constraints.IsHoliday(d)}) ===");
            var asgs = solution.Assignments.Values.Where(a => a.Date.Date == d.Date).OrderBy(a => a.ShiftLabel).ThenBy(a => a.UserId).ToList();
            foreach (var a in asgs)
            {
                var u = constraints.UserConstraints.First(x => x.UserId == a.UserId);
                var isReq = u.RequiredShiftSlots.Any(r => r.Date.Date == d.Date && r.ShiftLabel == a.ShiftLabel);
                var isSkel = solution.IsLockedSkeleton(a.UserId, a.ShiftId, a.Date) || a.IsSkeleton;
                _output.WriteLine($"  {a.ShiftLabel}: User {a.UserId} ({u.UserName}, L={u.ShiftManagerLevel}, Spec={u.SpecialtyId}) | IsReq={isReq}, IsSkel={isSkel}");
            }
        }
    }

    [Fact]
    public void Test_InspectShahrivar3AndMornings()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var solution = scheduler.Optimize();

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution);
        if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
        {
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
        }
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);

        _output.WriteLine("=== DAILY ASSIGNMENT COUNTS ===");
        var pDate = new System.Globalization.PersianCalendar();
        for (var d = constraints.StartDate.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
        {
            var dayAsgs = solution.Assignments.Values.Where(a => a.Date.Date == d.Date && !a.IsOnCall).ToList();
            var m = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Morning);
            var e = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Evening);
            var n = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Night);
            var isHol = constraints.IsHoliday(d);
            var shDate = $"{d:yyyy-MM-dd}";
            var pDay = $"{pDate.GetYear(d)}/{pDate.GetMonth(d):D2}/{pDate.GetDayOfMonth(d):D2}";
            _output.WriteLine($"{pDay} ({shDate}, Hol={isHol}): Total={dayAsgs.Count} (M={m}, E={e}, N={n})");
            if (pDay.EndsWith("03"))
            {
                foreach (var a in dayAsgs.OrderBy(a => a.ShiftLabel))
                {
                    var u = constraints.UserConstraints.First(x => x.UserId == a.UserId);
                    _output.WriteLine($"   {a.ShiftLabel}: User {a.UserId} ({u.UserName}, L={u.ShiftManagerLevel})");
                }
            }
        }

        _output.WriteLine("\n=== MORNING SHIFTS PER USER ===");
        foreach (var u in constraints.UserConstraints.OrderBy(u => u.UserId))
        {
            var asgs = solution.GetUserAllAssignments(u.UserId).Where(a => !a.IsOnCall).ToList();
            var m = asgs.Count(a => a.ShiftLabel == ShiftLabel.Morning);
            var e = asgs.Count(a => a.ShiftLabel == ShiftLabel.Evening);
            var n = asgs.Count(a => a.ShiftLabel == ShiftLabel.Night);
            _output.WriteLine($"User {u.UserId} ({u.UserName}, L={u.ShiftManagerLevel}): M={m}, E={e}, N={n}, Tot={asgs.Count}");
        }
    }

    [Fact]
    public void Test_InspectMissingMornings()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var solution = scheduler.Optimize();

        var d0904 = new DateTime(2026, 9, 4);
        var mShift = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Morning);
        void LogStages(string stage)
        {
            var m0904 = solution.GetShiftAssignments(mShift.ShiftId, d0904).Where(a => !a.IsOnCall).Select(a => $"{a.UserId}({constraints.UserConstraints.First(u => u.UserId == a.UserId).UserName})").ToList();
            _output.WriteLine($"[{stage}] 2026-09-04 Morning count = {m0904.Count}: {string.Join(", ", m0904)}");
        }

        LogStages("After Optimize");

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        LogStages("After ForceSatisfyAllDeficits 1");

        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        LogStages("After GlobalRebalanceNightQuotas 1");

        ExactNightQuotaGuard.Enforce(solution, constraints);
        LogStages("After Enforce 1");

        scheduler.PerformFinalManagerMixRepairSweep(solution);
        LogStages("After ManagerMixSweep");

        if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
        {
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
            LogStages("After Second Repair");
        }

        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        LogStages("After StripExcessCoverage");

        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        LogStages("After FillRemainingAfterForceApply");
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        LogStages("After Final StripExcessCoverage");

        MorningEveningBalanceGuard.Enforce(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);

        _output.WriteLine("\n=== ALL USERS SHIFT COUNTS ===");
        foreach (var u in constraints.UserConstraints.OrderBy(u => u.UserId))
        {
            var asgs = solution.GetUserAllAssignments(u.UserId).Where(a => !a.IsOnCall).ToList();
            var m = asgs.Count(a => a.ShiftLabel == ShiftLabel.Morning);
            var e = asgs.Count(a => a.ShiftLabel == ShiftLabel.Evening);
            var n = asgs.Count(a => a.ShiftLabel == ShiftLabel.Night);
            var holM = asgs.Count(a => a.ShiftLabel == ShiftLabel.Morning && constraints.IsHoliday(a.Date));
            var holE = asgs.Count(a => a.ShiftLabel == ShiftLabel.Evening && constraints.IsHoliday(a.Date));
            _output.WriteLine($"User {u.UserId} ({u.UserName}, L={u.ShiftManagerLevel}, Exp={u.ExperienceYears}): M={m}, E={e}, N={n}, Tot={asgs.Count} (HolM={holM}, HolE={holE})");
        }

        var dates = new[] { new DateTime(2026, 9, 4), new DateTime(2026, 9, 7) };
        foreach (var d in dates)
        {
            _output.WriteLine($"\n=== Date {d:yyyy-MM-dd} (Hol={constraints.IsHoliday(d)}) ===");
            var asgs = solution.Assignments.Values.Where(a => a.Date.Date == d.Date && !a.IsOnCall).OrderBy(a => a.ShiftLabel).ThenBy(a => a.UserId).ToList();
            _output.WriteLine($"Total: {asgs.Count} (M={asgs.Count(a => a.ShiftLabel == ShiftLabel.Morning)}, E={asgs.Count(a => a.ShiftLabel == ShiftLabel.Evening)}, N={asgs.Count(a => a.ShiftLabel == ShiftLabel.Night)})");
            foreach (var a in asgs)
            {
                var u = constraints.UserConstraints.First(x => x.UserId == a.UserId);
                _output.WriteLine($"  {a.ShiftLabel}: User {a.UserId} ({u.UserName}, L={u.ShiftManagerLevel})");
            }

            _output.WriteLine("\n  Checking ALL users for Morning on this date:");
            var mSpec = mShift.SpecialtyRequirements.First();
            foreach (var u in constraints.UserConstraints.OrderBy(u => u.UserId))
            {
                var reasons = "";
                if (u.UnavailableDates.Any(x => x.Date == d.Date)) reasons += "UnavailDate; ";
                if (u.UnavailableShiftSlots.Any(x => x.Date.Date == d.Date && x.ShiftLabel == ShiftLabel.Morning)) reasons += "UnavailSlot; ";
                if (solution.HasAssignment(u.UserId, mShift.ShiftId, d.Date)) reasons += "AlreadyHasMorning; ";
                var userDayAsgs = solution.GetUserAssignments(u.UserId, d.Date).Where(a => !a.IsOnCall).ToList();
                if (userDayAsgs.Any()) reasons += $"HasOtherShiftToday({string.Join(",", userDayAsgs.Select(a => a.ShiftLabel))}); ";
                var prevDayNight = solution.GetUserAssignments(u.UserId, d.Date.AddDays(-1)).Any(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                if (prevDayNight) reasons += "PrevDayNight; ";

                var mayEver = ShiftEligibilityResolver.MayEverTakeLabel(u, ShiftLabel.Morning);
                if (!mayEver) reasons += "MayEver=False; ";
                var eligible = DayShiftQuotaEligibility.CanAssignInCoverageFill(solution, constraints, u, ShiftLabel.Morning, d)
                    && ComboShiftQuotaEligibility.CanAssignInCoverageFill(solution, constraints, u, ShiftLabel.Morning, d);
                if (!eligible) reasons += "EligibleForCoverageFill=False; ";
                var wouldExceedMaxCons = MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, u, d);
                if (wouldExceedMaxCons) reasons += "WouldExceedMaxConsecutive; ";
                var wouldConflict = AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(u.UserId), d, ShiftLabel.Morning, constraints);
                if (wouldConflict) reasons += "AdjacentConflict; ";

                _output.WriteLine($"    User {u.UserId} ({u.UserName}): {(string.IsNullOrEmpty(reasons) ? "CAN TAKE MORNING!" : reasons)}");
            }
        }
    }

    [Fact]
    public void Test_InspectUser23Swaps()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var solution = scheduler.Optimize();

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        MorningEveningBalanceGuard.Enforce(solution, constraints);
        OvertimeBalanceGuard.Enforce(solution, constraints);
        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);

        _output.WriteLine("\n=== ALL USERS SHIFT COUNTS AFTER OPTIMIZE & ENFORCE ===");
        foreach (var u in constraints.UserConstraints.OrderBy(u => u.UserId))
        {
            var asgs = solution.GetUserAllAssignments(u.UserId).Where(a => !a.IsOnCall).ToList();
            var m = asgs.Count(a => a.ShiftLabel == ShiftLabel.Morning);
            var e = asgs.Count(a => a.ShiftLabel == ShiftLabel.Evening);
            var n = asgs.Count(a => a.ShiftLabel == ShiftLabel.Night);
            _output.WriteLine($"User {u.UserId} ({u.UserName}, L={u.ShiftManagerLevel}): M={m}, E={e}, N={n}, Tot={asgs.Count}");
        }

        _output.WriteLine("\n=== RUNNING ALL SYSTEM VALIDATIONS ===");
        var overCapViolations = ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints);
        _output.WriteLine($"OverCapacity violations: {overCapViolations.Count}");
        foreach (var v in overCapViolations) _output.WriteLine($"  {v}");

        var managerMixViolations = ShiftManagerMixGuard.GetViolations(solution, constraints);
        _output.WriteLine($"ManagerMix violations: {managerMixViolations.Count}");
        foreach (var v in managerMixViolations) _output.WriteLine($"  {v}");

        var restViolations = AdjacentShiftRestGuard.GetViolations(solution, constraints);
        _output.WriteLine($"AdjacentShiftRest violations: {restViolations.Count}");
        foreach (var v in restViolations) _output.WriteLine($"  {v}");

        var consecViolations = MaxConsecutiveWorkdayRules.GetViolations(solution, constraints);
        _output.WriteLine($"MaxConsecutive violations: {consecViolations.Count}");
        foreach (var v in consecViolations) _output.WriteLine($"  {v}");

        var unmetViolations = ApprovedRequestGuard.GetUnmetViolations(solution, constraints);
        _output.WriteLine($"Unmet approved request violations: {unmetViolations.Count}");
        foreach (var v in unmetViolations) _output.WriteLine($"  {v}");

        scheduler.AreExactNightQuotasSatisfied(solution, out var nightDeficits);
        _output.WriteLine($"Night quota deficits: {nightDeficits.Count}");
        foreach (var d in nightDeficits) _output.WriteLine($"  {d}");

        // Day by day coverage
        var totalDays = (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1;
        var underCoveredDays = 0;
        for (var i = 0; i < totalDays; i++)
        {
            var curDate = constraints.StartDate.Date.AddDays(i);
            var isHol = constraints.IsHoliday(curDate);
            var expectedTotal = isHol ? 10 : 11;
            var dayAsgs = solution.Assignments.Values.Where(a => a.Date.Date == curDate.Date && !a.IsOnCall).ToList();
            if (dayAsgs.Count != expectedTotal)
            {
                underCoveredDays++;
                _output.WriteLine($"WARNING: Date {curDate:yyyy-MM-dd} (Hol={isHol}) has {dayAsgs.Count} shifts (expected {expectedTotal})");
            }
        }
        _output.WriteLine($"Undercovered days count: {underCoveredDays}");

        Assert.Empty(overCapViolations);
        Assert.Empty(managerMixViolations);
        Assert.Empty(restViolations);
        Assert.Empty(nightDeficits);
        Assert.Equal(0, underCoveredDays);
    }
}


