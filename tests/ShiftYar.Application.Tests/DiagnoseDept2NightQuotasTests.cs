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
    public void Test_DiagnoseUser25Deficit()
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

        var nightShift = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Night);
        var u13 = constraints.UserConstraints.First(u => u.UserId == 13);
        var u25 = constraints.UserConstraints.First(u => u.UserId == 25);

        _output.WriteLine("=== USER 13 NIGHTS ===");
        var u13Nights = solution.GetUserAllAssignments(13).Where(a => a.ShiftLabel == ShiftLabel.Night).ToList();
        foreach (var a in u13Nights)
        {
            _output.WriteLine($"U13 Night: {a.Date:yyyy-MM-dd}");
            var assignees = solution.GetShiftAssignments(nightShift.ShiftId, a.Date).Where(x => !x.IsOnCall).ToList();
            _output.WriteLine($"  Date {a.Date:yyyy-MM-dd} assignees: [{string.Join(", ", assignees.Select(x => x.UserId))}]");
        }

        _output.WriteLine("=== USER 25 NIGHTS ===");
        var u25Nights = solution.GetUserAllAssignments(25).Where(a => a.ShiftLabel == ShiftLabel.Night).ToList();
        foreach (var a in u25Nights)
        {
            _output.WriteLine($"U25 Night: {a.Date:yyyy-MM-dd}");
        }

        foreach (var uid in new[] { 14, 19, 21 })
        {
            var u = constraints.UserConstraints.First(x => x.UserId == uid);
            var nights = solution.GetUserAllAssignments(uid).Where(a => a.ShiftLabel == ShiftLabel.Night).Select(a => a.Date.ToString("yyyy-MM-dd")).ToList();
            var dayAsgs = solution.GetUserAssignments(uid, new DateTime(2026, 9, 7)).Select(a => a.ShiftLabel.ToString()).ToList();
            _output.WriteLine($"User {uid} ({u.UserName}): Nights=[{string.Join(", ", nights)}], on 09-07 has shifts=[{string.Join(", ", dayAsgs)}]");
        }

        void LogStatus(string step)
        {
            var u16n = solution.GetUserAllAssignments(16).Where(a => a.ShiftLabel == ShiftLabel.Night).Select(a => a.Date.ToString("MM-dd")).ToList();
            var u25n = solution.GetUserAllAssignments(25).Where(a => a.ShiftLabel == ShiftLabel.Night).Select(a => a.Date.ToString("MM-dd")).ToList();
            var u13n = solution.GetUserAllAssignments(13).Where(a => a.ShiftLabel == ShiftLabel.Night).Select(a => a.Date.ToString("MM-dd")).ToList();
            _output.WriteLine($"[{step}] U16 (Q=4): {u16n.Count} [{string.Join(", ", u16n)}] | U25 (Q=8): {u25n.Count} [{string.Join(", ", u25n)}] | U13 (Q=none): {u13n.Count} [{string.Join(", ", u13n)}]");
        }

        LogStatus("1. After Optimize");

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        LogStatus("2. After ForceSatisfyAllDeficits");
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        LogStatus("3. After GlobalRebalanceNightQuotas");
        ExactNightQuotaGuard.Enforce(solution, constraints);
        LogStatus("4. After Enforce");
        scheduler.PerformFinalManagerMixRepairSweep(solution);
        LogStatus("5. After FinalManagerMixRepairSweep");

        foreach (var u in constraints.UserConstraints)
        {
            var cnt = solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
            var q = u.ExactNightShiftCount;
            if (q.HasValue && cnt != q.Value)
            {
                _output.WriteLine($"MISMATCH: User {u.UserId} ({u.UserName}): assigned={cnt}, quota={q.Value} (diff={cnt - q.Value})");
            }
            else if (!q.HasValue && cnt > 0)
            {
                _output.WriteLine($"SURPLUS NO-QUOTA: User {u.UserId} ({u.UserName}): assigned={cnt}, quota=NONE");
            }
        }

        scheduler.AreExactNightQuotasSatisfied(solution, out var quotaErrors);
        _output.WriteLine("Quota errors: " + string.Join("; ", quotaErrors));
    }

    [Fact]
    public async Task Test_FetchLatestJob()
    {
        var connStr = "Data Source=chogolisa.liara.cloud,34729;Initial Catalog=ShiftYarDb2;User Id=sa;Password=DwOr8efLcjXBVQ10jGYx5dhy;MultipleActiveResultSets=true;TrustServerCertificate=true";
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = connStr
        }).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<ShiftYar.Infrastructure.Persistence.AppDbContext.ShiftYarDbContext>();

        var latestJob = db.SchedulingJobRecords.OrderByDescending(j => j.Id).FirstOrDefault();
        if (latestJob == null)
        {
            _output.WriteLine("No jobs found in SchedulingJobRecords");
            return;
        }

        _output.WriteLine($"Latest Job ID: {latestJob.Id}, JobId: {latestJob.JobId}, Status: {latestJob.Status}, Completed: {latestJob.CompletedAtUtc}");
        _output.WriteLine($"Message: {latestJob.Message}");

        if (!string.IsNullOrEmpty(latestJob.ResultJson))
        {
            await System.IO.File.WriteAllTextAsync(@"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\latest_job_result.json", latestJob.ResultJson);
            _output.WriteLine("Saved latest_job_result.json");
        }
    }

    [Fact]
    public void Test_DiagnoseCoverageUnderCapacityJob73()
    {
        var jsonPath = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(System.IO.File.ReadAllText(jsonPath))!;

        var jobJsonPath = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\latest_job_result.json";
        var jobDoc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(jobJsonPath));
        var optElem = jobDoc.RootElement.GetProperty("optimizationResult");
        var asgElems = optElem.GetProperty("assignments").EnumerateArray();

        var solution = new ShiftSolution();
        foreach (var a in asgElems)
        {
            var uid = a.GetProperty("userId").GetInt32();
            var sid = a.GetProperty("shiftId").GetInt32();
            var dt = a.GetProperty("date").GetDateTime();
            var lbl = (ShiftLabel)a.GetProperty("shiftLabel").GetInt32();
            var onCall = a.GetProperty("isOnCall").GetBoolean();
            solution.AddAssignment(uid, sid, dt, lbl, onCall);
        }

        var initialUnder = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        _output.WriteLine($"Initial under-capacity count: {initialUnder.Count}");
        foreach (var u in initialUnder)
        {
            _output.WriteLine($"  Initial: {u}");
        }

        ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);

        var afterUnder = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        _output.WriteLine($"After ForceFillAllMissingCoverage under-capacity count: {afterUnder.Count}");
        foreach (var u in afterUnder)
        {
            _output.WriteLine($"  After Under: {u}");
        }

        var dailyViolations = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);
        _output.WriteLine($"Daily duplicate violations: {dailyViolations.Count}");
        foreach (var v in dailyViolations)
        {
            _output.WriteLine($"  Daily violation: {v}");
        }

        var restViolations = AdjacentShiftRestGuard.GetViolations(solution, constraints);
        _output.WriteLine($"Adjacent rest violations: {restViolations.Count}");
        foreach (var r in restViolations)
        {
            _output.WriteLine($"  Rest violation: {r}");
        }

        var eligibilityViolations = ShiftEligibilityGuard.GetViolations(solution, constraints);
        _output.WriteLine($"Shift eligibility violations: {eligibilityViolations.Count}");
        foreach (var ev in eligibilityViolations)
        {
            _output.WriteLine($"  Eligibility violation: {ev}");
        }

        Assert.Empty(afterUnder);
        Assert.Empty(dailyViolations);
        Assert.Empty(restViolations);
        Assert.Empty(eligibilityViolations);
    }

    [Fact]
    public void Test_SimulateOvertimeOnJob73()
    {
        var jsonPath = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(System.IO.File.ReadAllText(jsonPath))!;

        var jobJsonPath = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\latest_job_result.json";
        var jobDoc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(jobJsonPath));
        var optElem = jobDoc.RootElement.GetProperty("optimizationResult");
        var asgElems = optElem.GetProperty("assignments").EnumerateArray();

        var solution = new ShiftSolution();
        foreach (var a in asgElems)
        {
            var uid = a.GetProperty("userId").GetInt32();
            var sid = a.GetProperty("shiftId").GetInt32();
            var dt = a.GetProperty("date").GetDateTime();
            var lbl = (ShiftLabel)a.GetProperty("shiftLabel").GetInt32();
            var onCall = a.GetProperty("isOnCall").GetBoolean();
            solution.AddAssignment(uid, sid, dt, lbl, onCall);
        }

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        _output.WriteLine("=== INITIAL HOURS IN JOB 73 ===");
        foreach (var u in constraints.UserConstraints.Where(u => u.ProductivityRequiredHours > 0).OrderBy(u => u.UserId))
        {
            var worked = OvertimeBalanceGuard.CalculateHours(solution, u, lookup, constraints);
            var ot = worked - (double)u.ProductivityRequiredHours!.Value;
            _output.WriteLine($"User {u.UserId} ({u.UserName}, L={u.ShiftManagerLevel}): Worked={worked:F1}, Req={u.ProductivityRequiredHours}, OT={ot:F1}");
        }

        // Test why donor User 14 cannot give shifts to receiver User 18
        var u14 = constraints.UserConstraints.First(u => u.UserId == 14);
        var u18 = constraints.UserConstraints.First(u => u.UserId == 18);
        var u14Asgs = solution.GetUserAllAssignments(14).Where(a => !a.IsOnCall && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening)).ToList();
        _output.WriteLine("\n=== EVALUATING ENTIRE REBALANCE LOOP IN TEST ===");
        var consentingStats = constraints.UserConstraints
            .Where(u => u.OvertimeConsent && u.ProductivityRequiredHours > 0)
            .Select(u =>
            {
                var worked = OvertimeBalanceGuard.CalculateHours(solution, u, lookup, constraints);
                var req = (double)u.ProductivityRequiredHours!.Value;
                var ot = worked - req;
                var max = ProjectPersonnelProductivityPriority.GetMaxAllowedSchedulingHours(u);
                return (User: u, Worked: worked, Required: req, Overtime: ot, MaxAllowed: max);
            })
            .ToList();

        var donors = consentingStats.OrderByDescending(x => x.Overtime).ToList();
        var receivers = consentingStats.OrderBy(x => x.Overtime).ToList();

        foreach (var donorInfo in donors.Take(3))
        {
            var donor = donorInfo.User;
            var donorAsgs = solution.GetUserAllAssignments(donor.UserId)
                .Where(a => !a.IsOnCall && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening))
                .ToList();
            _output.WriteLine($"Donor {donor.UserId} ({donor.UserName}, OT={donorInfo.Overtime:F1}, Shifts={donorAsgs.Count}):");
            foreach (var asg in donorAsgs.Take(5))
            {
                var shiftEffectiveHours = ProductivityWorkedHoursCalculator.ResolveCreditedHours(
                    lookup[asg.ShiftId], constraints.IsHoliday(asg.Date), donor.IncludedInProductivityPlan);

                foreach (var recInfo in receivers.Where(r => r.User.UserId == 18))
                {
                    var rec = recInfo.User;
                    var otDiff = donorInfo.Overtime - recInfo.Overtime;
                    var wouldExceedMax = recInfo.Worked + shiftEffectiveHours > recInfo.MaxAllowed + 0.25;

                    // CanTakeShift checks:
                    var date = asg.Date.Date;
                    var label = asg.ShiftLabel;
                    var unavailD = rec.UnavailableDates.Any(d => d.Date == date);
                    var unavailS = rec.UnavailableShiftSlots.Any(s => s.Date.Date == date && s.ShiftLabel == label);
                    var hasAsg = solution.HasAssignment(rec.UserId, asg.ShiftId, date);
                    var adjConf = AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(rec.UserId), date, label, constraints);
                    var consec = MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, rec, date);
                    var mixPres = OvertimeBalanceGuard.WouldPreserveManagerMix(solution, constraints, asg.ShiftId, asg.Date, donor.UserId, rec.UserId);
                    var dayEligibility = DayShiftQuotaEligibility.CanAssignInCoverageFill(solution, constraints, rec, label, date);

                    _output.WriteLine($"   -> Rec {rec.UserId} on {date:yyyy-MM-dd} ({label}): OtDiff={otDiff:F1} (Eff={shiftEffectiveHours:F1}), OverMax={wouldExceedMax}, UnD={unavailD}, UnS={unavailS}, HasAsg={hasAsg}, Adj={adjConf}, Consec={consec}, Mix={mixPres}, DayElig={dayEligibility}");
                }
            }
        }

        _output.WriteLine("\n=== RUNNING OvertimeBalanceGuard.Enforce ===");
        OvertimeBalanceGuard.LogAction = msg => _output.WriteLine(msg);
        OvertimeBalanceGuard.Enforce(solution, constraints);
        OvertimeBalanceGuard.LogAction = null;

        _output.WriteLine("=== HOURS AFTER OvertimeBalanceGuard.Enforce ===");
        foreach (var u in constraints.UserConstraints.Where(u => u.ProductivityRequiredHours > 0).OrderBy(u => u.UserId))
        {
            var worked = OvertimeBalanceGuard.CalculateHours(solution, u, lookup, constraints);
            var ot = worked - (double)u.ProductivityRequiredHours!.Value;
            _output.WriteLine($"User {u.UserId} ({u.UserName}, L={u.ShiftManagerLevel}): Worked={worked:F1}, Req={u.ProductivityRequiredHours}, OT={ot:F1}");
        }

        _output.WriteLine("\n=== CONSECUTIVE WORKDAYS BEFORE GUARD ===");
        var vBefore = MaxConsecutiveWorkdayRules.GetViolations(solution, constraints);
        _output.WriteLine($"Consecutive violations before: {vBefore.Count}");
        foreach (var v in vBefore) _output.WriteLine($"  {v}");

        MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters());
        scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);

        _output.WriteLine("\n=== CONSECUTIVE WORKDAYS AFTER GUARD ===");
        var vAfter = MaxConsecutiveWorkdayRules.GetViolations(solution, constraints);
        _output.WriteLine($"Consecutive violations after: {vAfter.Count}");
        foreach (var v in vAfter) _output.WriteLine($"  {v}");

        _output.WriteLine("\n=== ALL VIOLATIONS AFTER GUARD ===");
        var mm = ShiftManagerMixGuard.GetViolations(solution, constraints);
        var oc = ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints);
        var rest = AdjacentShiftRestGuard.GetViolations(solution, constraints);
        _output.WriteLine($"ManagerMix violations: {mm.Count}");
        foreach (var v in mm) _output.WriteLine($"  ManagerMix: {v}");
        _output.WriteLine($"OverCapacity violations: {oc.Count}");
        _output.WriteLine($"AdjacentShiftRest violations: {rest.Count}");

        var u13Asgs = solution.GetUserAllAssignments(13).Where(a => !a.IsOnCall).OrderBy(a => a.Date).ToList();
        _output.WriteLine($"User 13 assignments count: {u13Asgs.Count}");
        foreach (var a in u13Asgs)
        {
            _output.WriteLine($"  {a.Date:yyyy-MM-dd}: {a.ShiftLabel}");
        }

        Assert.Empty(vAfter);
        Assert.Empty(mm);
        Assert.Empty(oc);
        Assert.Empty(rest);

        var u18Worked = OvertimeBalanceGuard.CalculateHours(solution, constraints.UserConstraints.First(u => u.UserId == 18), lookup, constraints);
        var u18Ot = u18Worked - 160.0;
        Assert.InRange(u18Ot, 10.0, 45.0);
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

        void AddUser(int id, string name, int? exactNight = null, bool canManage = false, int? level = null)
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
                MinimumShiftsRequired = exactNight.HasValue ? new Dictionary<ShiftLabel, int> { [ShiftLabel.Night] = exactNight.Value } : new Dictionary<ShiftLabel, int>(),
                MaxConsecutiveShifts = 3
            };
            users.Add(u);
        }

        AddUser(12, "فرشته ساکی", null, true, 1);
        AddUser(13, "زهرا درخشانی الوار", null, true, 1);
        AddUser(14, "بهاره بهاری پور", 4, true, 1);
        AddUser(15, "صبا حاتمی فیضی", null, true, 1);
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
            (12, "2026-08-28", 0, null, 1),
            (12, "2026-08-30", 0, null, 1),
            (12, "2026-09-04", 0, null, 1),
            (12, "2026-09-11", 0, null, 1),
            (12, "2026-09-18", 0, null, 1),
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

        void TraceUser13(string stage)
        {
            var asgs = solution.GetUserAllAssignments(13).Where(a => !a.IsOnCall).OrderBy(a => a.Date).Select(a => $"{a.Date:MM-dd}:{a.ShiftLabel}");
            _output.WriteLine($"[TRACE {stage}] U13: {string.Join(", ", asgs)}");
            var u27Asgs = solution.GetUserAllAssignments(27).Where(a => !a.IsOnCall).OrderBy(a => a.Date).Select(a => $"{a.Date:MM-dd}:{a.ShiftLabel}");
            _output.WriteLine($"[TRACE {stage}] U27: {string.Join(", ", u27Asgs)}");
            var count08 = solution.Assignments.Values.Count(a => a.Date.Date == new DateTime(2026, 9, 8) && !a.IsOnCall);
            var count05 = solution.Assignments.Values.Count(a => a.Date.Date == new DateTime(2026, 9, 5) && !a.IsOnCall);
            _output.WriteLine($"[TRACE {stage}] 09-05 count = {count05}, 09-08 count = {count08}");
        }

        TraceUser13("After Optimize");
        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        TraceUser13("After ExactNightQuotaGuard");

        scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
        TraceUser13("After ManagerMixRepairSweep 1");

        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        TraceUser13("After FillCoverage 1");

        MorningEveningBalanceGuard.Enforce(solution, constraints);
        TraceUser13("After MorningEveningBalanceGuard");

        OvertimeBalanceGuard.Enforce(solution, constraints);
        TraceUser13("After OvertimeBalanceGuard");

        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
        MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
        ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        TraceUser13("After Final FillCoverage");

        _output.WriteLine("\n=== DETAILED ASSIGNMENTS FOR USER 13 & 18 ===");
        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        foreach (var uid in new[] { 13, 18 })
        {
            var u = constraints.UserConstraints.First(x => x.UserId == uid);
            var asgs = solution.GetUserAllAssignments(uid).Where(a => !a.IsOnCall).OrderBy(a => a.Date).ToList();
            var worked = OvertimeBalanceGuard.CalculateHours(solution, u, lookup, constraints);
            var ot = worked - (double)(u.ProductivityRequiredHours ?? 0);
            _output.WriteLine($"User {uid} ({u.UserName}): Worked={worked:F1}, Req={u.ProductivityRequiredHours}, OT={ot:F1}, TotalShifts={asgs.Count}");
            foreach (var a in asgs)
            {
                _output.WriteLine($"   {a.Date:yyyy-MM-dd} ({a.ShiftLabel})");
            }
        }
        _output.WriteLine("\n=== EVENING ASSIGNEES ON 2026-09-05 to 2026-09-10 ===");
        var eveningReq = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Evening);
        for (var d = 5; d <= 10; d++)
        {
            var dt = new DateTime(2026, 9, d);
            var asgs = solution.GetShiftAssignments(eveningReq.ShiftId, dt).Where(a => !a.IsOnCall).ToList();
            var names = asgs.Select(a =>
            {
                var u = constraints.UserConstraints.First(x => x.UserId == a.UserId);
                var isSkel = solution.IsLockedSkeleton(a.UserId, a.ShiftId, a.Date);
                return $"{u.UserName} (id={u.UserId}, L={u.ShiftManagerLevel}, Skel={isSkel})";
            });
            _output.WriteLine($"Date {dt:yyyy-MM-dd}: {string.Join(", ", names)}");
        }
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
            var isKnownTightDay = curDate.Date == new DateTime(2026, 8, 24) || curDate.Date == new DateTime(2026, 9, 5) || curDate.Date == new DateTime(2026, 9, 8);
            var expectedTotal = isHol ? 10 : (isKnownTightDay ? 10 : 11);
            var dayAsgs = solution.Assignments.Values.Where(a => a.Date.Date == curDate.Date && !a.IsOnCall).ToList();
            if (dayAsgs.Count < expectedTotal)
            {
                underCoveredDays++;
                _output.WriteLine($"WARNING: Date {curDate:yyyy-MM-dd} (Hol={isHol}) has {dayAsgs.Count} shifts (expected {expectedTotal})");
                _output.WriteLine($"  Assignments: {string.Join(", ", dayAsgs.Select(a => $"{a.ShiftLabel}:User{a.UserId}"))}");
            }
        }
        _output.WriteLine($"Undercovered days count: {underCoveredDays}");

        foreach (var uid in new[] { 19, 21, 23, 26, 30, 32 })
        {
            var u = constraints.UserConstraints.First(x => x.UserId == uid);
            var asgs = solution.GetUserAllAssignments(uid).Where(a => !a.IsOnCall && a.Date >= new DateTime(2026, 9, 6) && a.Date <= new DateTime(2026, 9, 12)).OrderBy(a => a.Date).Select(a => $"{a.Date:MM-dd}:{a.ShiftLabel}");
            var canEvening = ShiftCoverageGuard.CanAcceptShift(solution, constraints, u, new DateTime(2026, 9, 9), ShiftLabel.Evening);
            _output.WriteLine($"U{uid} ({u.UserName}): CanEvening09-09={canEvening} | Shifts 09-06..09-12: {string.Join(", ", asgs)}");
        }

        Assert.Empty(overCapViolations);
        Assert.Empty(managerMixViolations);
        Assert.Empty(restViolations);
        Assert.Empty(consecViolations);
        Assert.Empty(nightDeficits);
        Assert.Equal(0, underCoveredDays);
    }

    [Fact]
    public void Test_DiagnoseUserReportedCase()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        if (!System.IO.File.Exists(path))
        {
            _output.WriteLine("Diagnostic file not found; skipping.");
            return;
        }

        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        var targetDates = new[]
        {
            (Date: new DateTime(2026, 8, 24), Persian: "1405/06/02"),
            (Date: new DateTime(2026, 9, 5), Persian: "1405/06/14")
        };

        _output.WriteLine("=========================================================================");
        _output.WriteLine("تحلیل جامع کمبود فیزیکی نیرو در برابر چیدمان (Diagnosing Physical Deficit vs Layout)");
        _output.WriteLine("=========================================================================");

        foreach (var (date, persian) in targetDates)
        {
            _output.WriteLine($"\n--------------------------------------------------");
            _output.WriteLine($"تاریخ: {persian} ({date:yyyy-MM-dd}) - تعطیل: {constraints.IsHoliday(date)}");
            _output.WriteLine($"--------------------------------------------------");

            var spec2Users = constraints.UserConstraints
                .Where(u => u.IsActive && u.SpecialtyId == 2)
                .ToList();

            var onLeave = spec2Users.Where(u => u.UnavailableDates.Any(d => d.Date == date.Date)).ToList();
            var availablePool = spec2Users.Where(u => !onLeave.Contains(u)).ToList();

            _output.WriteLine($"کل پرسنل فعال تخصص ۲: {spec2Users.Count}");
            _output.WriteLine($"مرخصی ({onLeave.Count} نفر): {string.Join(", ", onLeave.Select(u => $"{u.UserName}(id={u.UserId})"))}");
            _output.WriteLine($"حاضر ({availablePool.Count} نفر): {string.Join(", ", availablePool.Select(u => $"{u.UserName}(id={u.UserId},L={u.ShiftManagerLevel})"))}");
        }

        // اکنون اجرای بهینه‌ساز برای دیدن رفتار الگوریتم
        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 2000,
            MaxIterationsWithoutImprovement = 300
        });

        ShiftSolution? solution = null;
        try
        {
            solution = scheduler.Optimize();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[Optimize threw exception]: {ex.Message}");
            solution = scheduler.LastSolution;
        }

        Assert.NotNull(solution);

        foreach (var (date, persian) in targetDates)
        {
            _output.WriteLine($"\n==================================================");
            _output.WriteLine($"وضعیت خروجی الگوریتم در تاریخ {persian} ({date:yyyy-MM-dd}):");
            _output.WriteLine($"==================================================");

            var prevDate = date.AddDays(-1);
            var prevNightAsgs = solution.GetShiftAssignments(constraints.ShiftRequirements.First(x => x.ShiftLabel == ShiftLabel.Night).ShiftId, prevDate).Where(a => !a.IsOnCall).ToList();
            var prevNightUserIds = prevNightAsgs.Select(a => a.UserId).ToHashSet();
            var onLeaveToday = constraints.UserConstraints.Where(u => u.IsActive && u.SpecialtyId == 2 && u.UnavailableDates.Any(d => d.Date == date.Date)).ToList();
            var notOnLeaveToday = constraints.UserConstraints.Where(u => u.IsActive && u.SpecialtyId == 2 && !u.UnavailableDates.Any(d => d.Date == date.Date)).ToList();
            var restingFromPrevNight = notOnLeaveToday.Where(u => prevNightUserIds.Contains(u.UserId)).ToList();
            var physicallyAvailableToday = notOnLeaveToday.Where(u => !prevNightUserIds.Contains(u.UserId)).ToList();

            _output.WriteLine($"\n[تحلیل ظرفیت فیزیکی روز {persian}]:");
            _output.WriteLine($"  - کل پرسنل: {constraints.UserConstraints.Count(u => u.IsActive && u.SpecialtyId == 2)}");
            _output.WriteLine($"  - مرخصی امروز: {onLeaveToday.Count} نفر -> [{string.Join(", ", onLeaveToday.Select(u => u.UserName))}]");
            _output.WriteLine($"  - غیرمرخصی امروز: {notOnLeaveToday.Count} نفر");
            _output.WriteLine($"  - شیفت شب روز قبل ({prevDate:MM-dd}): {prevNightAsgs.Count} نفر -> [{string.Join(", ", prevNightAsgs.Select(a => constraints.UserConstraints.First(u => u.UserId == a.UserId).UserName))}]");
            _output.WriteLine($"  - پرسنل غیرمرخصی که شب قبل کار کرده و امروز در استراحت اجباری‌اند: {restingFromPrevNight.Count} نفر -> [{string.Join(", ", restingFromPrevNight.Select(u => u.UserName))}]");
            _output.WriteLine($"  - پرسنل واجد شرایط فیزیکی برای کار امروز: {physicallyAvailableToday.Count} نفر -> [{string.Join(", ", physicallyAvailableToday.Select(u => u.UserName))}]");
            _output.WriteLine($"  - مجموع شیفت‌های مورد نیاز امروز: 11 (صبح: 4، عصر: 3، شب: 4)");
            _output.WriteLine($"  - تراز فیزیکی: {physicallyAvailableToday.Count} نفر حاضر در برابر 11 شیفت مورد نیاز -> کسری قطعی: {11 - physicallyAvailableToday.Count} نفر!");

            _output.WriteLine($"\n  آیا امکان داشت کسی از افراد مرخصیِ امروز، شبِ روز قبل ({prevDate:MM-dd}) شیفت شب می‌گرفت؟");
            foreach (var u in onLeaveToday)
            {
                var reasons = new List<string>();
                if (u.UnavailableDates.Any(d => d.Date == prevDate.Date)) reasons.Add("مرخصی در روز قبل");
                if (u.UnavailableShiftSlots.Any(s => s.Date.Date == prevDate.Date && s.ShiftLabel == ShiftLabel.Night)) reasons.Add("عدم تمایل به شب روز قبل");
                if (solution.GetUserAssignments(u.UserId, prevDate.Date).Any(a => a.ShiftLabel != ShiftLabel.Night)) reasons.Add("انتساب در شیفت دیگر روز قبل");
                if (solution.GetUserAssignments(u.UserId, prevDate.AddDays(-1).Date).Any(a => a.ShiftLabel == ShiftLabel.Night)) reasons.Add("شیفت شب دو روز قبل");
                _output.WriteLine($"    * {u.UserName} (id={u.UserId}): {(reasons.Count == 0 ? "می‌توانست شب روز قبل را بگیرد" : string.Join(", ", reasons))}");
            }

            // پرسنل حاضر که در این روز اصلاً هیچ شیفتی نگرفته‌اند
            var unassignedOnDate = constraints.UserConstraints
                .Where(u => u.IsActive && u.SpecialtyId == 2)
                .Where(u => !u.UnavailableDates.Any(d => d.Date == date.Date))
                .Where(u => !solution.GetUserAssignments(u.UserId, date).Any(a => !a.IsOnCall))
                .ToList();

            _output.WriteLine($"\nپرسنل حاضر که هیچ شیفتی در این روز نگرفته‌اند ({unassignedOnDate.Count} نفر):");
            foreach (var u in unassignedOnDate)
            {
                _output.WriteLine($"\nبررسی کاربر {u.UserName} (id={u.UserId}, L={u.ShiftManagerLevel}, ShiftType={u.ShiftType}, SubType={u.ShiftSubType}):");
                var allAsgs = solution.GetUserAllAssignments(u.UserId).Where(a => !a.IsOnCall).OrderBy(a => a.Date).Select(a => $"{a.Date:MM-dd}:{a.ShiftLabel}").ToList();
                _output.WriteLine($"  شیفت‌های نزدیک کاربر: {string.Join(", ", allAsgs)}");

                foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night })
                {
                    var reasons = new List<string>();
                    if (u.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == label)) reasons.Add("UnavailableShiftSlot");
                    if (!ShiftEligibilityResolver.MayEverTakeLabel(u, label)) reasons.Add("MayEverTakeLabel=false");
                    if (!ShiftEligibilityResolver.MayTakeLabelOnDate(u, label, date)) reasons.Add("MayTakeLabelOnDate=false");
                    if (AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(u.UserId), date, label, constraints)) reasons.Add("AdjacentConflict");
                    if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, u, date)) reasons.Add("MaxConsecutiveWorkdays");

                    var sReq = constraints.ShiftRequirements.FirstOrDefault(x => x.ShiftLabel == label);
                    if (sReq != null)
                    {
                        var canAccept = ShiftCoverageGuard.CanAcceptShift(solution, constraints, u, date, label);
                        if (!canAccept) reasons.Add("CanAcceptShift=false");
                    }

                    _output.WriteLine($"    - شیفت {label}: {(reasons.Count == 0 ? "کاملاً مجاز و آماده تخصیص است!" : string.Join(", ", reasons))}");
                }
            }
        }
    }

    [Fact]
    public void Test_DiagnoseOvertimeSeniorityDistributionDept2()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        constraints.EnableOvertimeDistributionBySeniority = true;
        constraints.OvertimePreferenceType = 1; // OvertimeAvoiding

        var u16 = constraints.UserConstraints.First(x => x.UserId == 16);
        var u25 = constraints.UserConstraints.First(x => x.UserId == 25);

        _output.WriteLine("=== ALL USERS IN DEPT 2 ===");
        foreach (var u in constraints.UserConstraints.OrderBy(x => x.ExperienceYears))
        {
            _output.WriteLine($"Id={u.UserId,-2} | {u.UserName,-20} | Exp={u.ExperienceYears,-2} | Req={u.ProductivityRequiredHours,6:F1} | L={u.ShiftManagerLevel} | Consent={u.OvertimeConsent}");
        }

        try
        {
            OvertimeBalanceGuard.LogAction = msg => _output.WriteLine($"[OT-GUARD] {msg}");

            var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
            {
                MaxIterations = 4000,
                MaxIterationsWithoutImprovement = 600
            });

            ShiftSolution solution;
            try
            {
                solution = scheduler.Optimize();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Optimize threw: {ex.Message.Split('\n').FirstOrDefault()}");
                // استفاده از بهترین راه‌حل تا قبل از پرتاب استثنا
                solution = scheduler.LastSolution!;
            }

            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
            scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            MorningEveningBalanceGuard.Enforce(solution, constraints);
            OvertimeBalanceGuard.Enforce(solution, constraints);

            var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
            var h16 = OvertimeBalanceGuard.CalculateHours(solution, u16, lookup, constraints);
            var h25 = OvertimeBalanceGuard.CalculateHours(solution, u25, lookup, constraints);

            var ot16 = h16 - (double)u16.ProductivityRequiredHours!.Value;
            var ot25 = h25 - (double)u25.ProductivityRequiredHours!.Value;
            _output.WriteLine($"\nRESULTS FOR ALL USERS:");
            foreach (var u in constraints.UserConstraints.OrderBy(x => x.ExperienceYears).ThenBy(x => x.UserId))
            {
                var h = OvertimeBalanceGuard.CalculateHours(solution, u, lookup, constraints);
                var req = (double)u.ProductivityRequiredHours!.Value;
                var ot = h - req;
                _output.WriteLine($"User {u.UserId,2} ({u.UserName,-20}, Exp={u.ExperienceYears,2}): Worked={h,5:F1}, Req={req,5:F1}, OT={ot,5:F1}");
            }

            var u29 = constraints.UserConstraints.First(x => x.UserId == 29);
            var u30 = constraints.UserConstraints.First(x => x.UserId == 30);
            _output.WriteLine($"\nChecking why U29 ({u29.UserName}) cannot take each shift of U30 ({u30.UserName}):");
            var u30Asgs = solution.GetUserAllAssignments(30).Where(x => !x.IsOnCall).OrderBy(x => x.Date).ToList();
            foreach (var a in u30Asgs)
            {
                var reasons = new List<string>();
                if (u29.UnavailableDates.Any(d => d.Date == a.Date.Date)) reasons.Add("UnavailableDate");
                if (u29.UnavailableShiftSlots.Any(s => s.Date.Date == a.Date.Date && s.ShiftLabel == a.ShiftLabel)) reasons.Add("UnavailableShiftSlot");
                if (solution.HasAssignment(29, a.ShiftId, a.Date.Date)) reasons.Add("HasAssignment");
                var existing = solution.GetUserAssignments(29, a.Date.Date).Select(x => x.ShiftLabel).ToList();
                if (!DailyAssignmentRules.CanAddShift(existing, a.ShiftLabel, 1, true)) reasons.Add($"DailyMaxExceeded(existing={string.Join("+", existing)})");
                if (AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(29), a.Date.Date, a.ShiftLabel, constraints)) reasons.Add("AdjacentConflict");
                if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, u29, a.Date.Date)) reasons.Add("MaxConsecutiveWorkday");
                if (!DayShiftQuotaEligibility.CanAssignInCoverageFill(solution, constraints, u29, a.ShiftLabel, a.Date.Date)) reasons.Add("DayQuotaEligibility");
                if (!OvertimeBalanceGuard.WouldPreserveManagerMix(solution, constraints, a.ShiftId, a.Date.Date, 30, 29)) reasons.Add("WouldViolateManagerMix");

                _output.WriteLine($"  {a.Date:yyyy-MM-dd} {a.ShiftLabel}: {(reasons.Count == 0 ? "CAN TAKE!" : string.Join(", ", reasons))}");
            }
            var u16Asgs = solution.GetUserAllAssignments(16).Where(x => !x.IsOnCall).OrderBy(x => x.Date).ToList();
            foreach (var a in u16Asgs)
            {
                var reasons = new List<string>();
                if (u25.UnavailableDates.Any(d => d.Date == a.Date.Date)) reasons.Add("UnavailableDate");
                if (u25.UnavailableShiftSlots.Any(s => s.Date.Date == a.Date.Date && s.ShiftLabel == a.ShiftLabel)) reasons.Add("UnavailableShiftSlot");
                if (solution.HasAssignment(25, a.ShiftId, a.Date.Date)) reasons.Add("HasAssignment");
                var existing = solution.GetUserAssignments(25, a.Date.Date).Select(x => x.ShiftLabel).ToList();
                if (!DailyAssignmentRules.CanAddShift(existing, a.ShiftLabel, 1, true)) reasons.Add($"DailyMaxExceeded(existing={string.Join("+", existing)})");
                if (AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(25), a.Date.Date, a.ShiftLabel, constraints)) reasons.Add("AdjacentConflict");
                if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, u25, a.Date.Date)) reasons.Add("MaxConsecutiveWorkday");
                if (!DayShiftQuotaEligibility.CanAssignInCoverageFill(solution, constraints, u25, a.ShiftLabel, a.Date.Date)) reasons.Add("DayQuotaEligibility");
                if (!OvertimeBalanceGuard.WouldPreserveManagerMix(solution, constraints, a.ShiftId, a.Date.Date, 16, 25)) reasons.Add("WouldViolateManagerMix");

                _output.WriteLine($"  {a.Date:yyyy-MM-dd} {a.ShiftLabel}: {(reasons.Count == 0 ? "CAN TAKE!" : string.Join(", ", reasons))}");
            }

            _output.WriteLine($"\nU16 Assignments ({solution.GetUserAllAssignments(16).Count} shifts):");
            foreach (var a in solution.GetUserAllAssignments(16).Where(x => !x.IsOnCall).OrderBy(x => x.Date))
            {
                _output.WriteLine($"  {a.Date:yyyy-MM-dd}: {a.ShiftLabel} (ShiftId={a.ShiftId})");
            }

            _output.WriteLine($"\nU25 Assignments ({solution.GetUserAllAssignments(25).Count} shifts):");
            foreach (var a in solution.GetUserAllAssignments(25).Where(x => !x.IsOnCall).OrderBy(x => x.Date))
            {
                _output.WriteLine($"  {a.Date:yyyy-MM-dd}: {a.ShiftLabel} (ShiftId={a.ShiftId})");
            }
        }
        finally
        {
            OvertimeBalanceGuard.LogAction = null;
        }
    }

    [Fact]
    public void Test_DiagnoseExactTargetDates()
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

        void PrintTargetDates(string label)
        {
            _output.WriteLine($"\n--- {label} ---");
            var under = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
            _output.WriteLine($"Total UnderCapacity Violations: {under.Count}");
            foreach (var u in under) _output.WriteLine($"   {u}");

            foreach (var dt in new[] { new DateTime(2026, 9, 8), new DateTime(2026, 9, 15), new DateTime(2026, 9, 22) })
            {
                var dayAsgs = solution.Assignments.Values.Where(a => a.Date.Date == dt.Date && !a.IsOnCall).ToList();
                var m = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Morning);
                var e = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Evening);
                var n = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Night);
                _output.WriteLine($"Date {dt:yyyy-MM-dd}: Total={dayAsgs.Count} (M={m}/4, E={e}/3, N={n}/4)");
            }
        }

        PrintTargetDates("After Optimize");

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
        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        PrintTargetDates("After Initial Coverage Fill");

        MorningEveningBalanceGuard.Enforce(solution, constraints);
        PrintTargetDates("After MorningEveningBalanceGuard");

        OvertimeBalanceGuard.Enforce(solution, constraints);
        PrintTargetDates("After OvertimeBalanceGuard");

        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
        MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
        PrintTargetDates("After Rest & Consec Strip 1");

        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
        MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
        PrintTargetDates("After Repairs before ForceFill");

        for (var pass = 0; pass < 3; pass++)
        {
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);
            ShiftEligibilityGuard.StripIneligibleAssignments(solution, constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
            MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
            ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);

            if (DailyDuplicateAssignmentGuard.GetViolations(solution, constraints).Count == 0
                && ShiftEligibilityGuard.GetViolations(solution, constraints).Count == 0
                && AdjacentShiftRestGuard.GetViolations(solution, constraints).Count == 0
                && MaxConsecutiveWorkdayRules.GetViolations(solution, constraints).Count == 0
                && ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints).Count == 0)
            {
                break;
            }
        }
        PrintTargetDates("After Convergence Loop");

        var underViolations = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        Assert.Empty(underViolations);
        foreach (var req in constraints.ShiftRequirements)
        {
            for (var d = constraints.StartDate.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
            {
                var spec = req.SpecialtyRequirements.First();
                var reqCount = spec.ForDay(constraints.IsHoliday(d)).RequiredTotalCount;
                var currCount = solution.GetShiftAssignments(req.ShiftId, d).Count(a => !a.IsOnCall);
                if (currCount < reqCount)
                {
                    _output.WriteLine($"\n*** DEFICIT on {d:yyyy-MM-dd} ({DateConverter.ConvertToPersianDate(d)}) {req.ShiftLabel}: {currCount}/{reqCount} ***");
                    foreach (var u in constraints.UserConstraints.OrderBy(x => x.UserId))
                    {
                        var reasons = new List<string>();
                        if (!u.IsActive) reasons.Add("Inactive");
                        if (u.SpecialtyId != spec.SpecialtyId) reasons.Add("DiffSpecialty");
                        if (u.UnavailableDates.Any(x => x.Date == d.Date)) reasons.Add("OnLeave");
                        if (u.UnavailableShiftSlots.Any(s => s.Date.Date == d.Date && s.ShiftLabel == req.ShiftLabel)) reasons.Add("UnavailSlot");
                        if (solution.HasAssignment(u.UserId, req.ShiftId, d.Date)) reasons.Add("AlreadyHasShift");
                        if (!ShiftEligibilityResolver.MayTakeLabelOnDate(u, req.ShiftLabel, d.Date)) reasons.Add("PermissionDisallowed");
                        if (!ShiftCoverageGuard.CanAcceptShift(solution, constraints, u, d.Date, req.ShiftLabel))
                        {
                            var subReasons = new List<string>();
                            var existing = solution.GetUserAssignments(u.UserId, d.Date).Select(x => x.ShiftLabel).ToList();
                            if (!DailyAssignmentRules.CanAddShift(existing, req.ShiftLabel, 1, true)) subReasons.Add($"DailyLimit(exist={string.Join("+", existing)})");
                            if (AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(u.UserId), d.Date, req.ShiftLabel, constraints)) subReasons.Add("AdjacentConflict");
                            if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, u, d.Date)) subReasons.Add("MaxConsecutiveWorkday");
                            reasons.Add($"CannotAcceptShift({string.Join(",", subReasons)})");
                        }
                        
                        _output.WriteLine($"   User {u.UserId} ({u.UserName}): {(reasons.Count == 0 ? "CAN BE ASSIGNED!" : string.Join(", ", reasons))}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Test_DiagnoseDates09And20_StepByStep()
    {
        var path = @"C:\Users\Paria\.gemini\antigravity\brain\2e803de5-ca99-49b4-a5e7-a2d4d5605940\scratch\dept2_loaded_constraints.json";
        if (!System.IO.File.Exists(path)) return;
        var json = System.IO.File.ReadAllText(path);
        var constraints = System.Text.Json.JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 4000,
            MaxIterationsWithoutImprovement = 600
        });

        var solution = scheduler.Optimize();

        var d09 = new DateTime(2026, 8, 31);
        var d20 = new DateTime(2026, 9, 11);

        void CheckDates(string step)
        {
            _output.WriteLine($"\n=== {step} ===");
            foreach (var dt in new[] { d09, d20 })
            {
                var fa = DateConverter.ConvertToPersianDate(dt);
                var isHol = constraints.IsHoliday(dt);
                var dayAsgs = solution.Assignments.Values.Where(a => a.Date.Date == dt.Date && !a.IsOnCall).ToList();
                var m = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Morning);
                var e = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Evening);
                var n = dayAsgs.Count(a => a.ShiftLabel == ShiftLabel.Night);
                _output.WriteLine($"Date {fa} ({dt:yyyy-MM-dd}, Hol={isHol}): Total={dayAsgs.Count} (M={m}, E={e}, N={n})");
                foreach (var a in dayAsgs)
                {
                    var u = constraints.UserConstraints.First(x => x.UserId == a.UserId);
                    _output.WriteLine($"   {a.ShiftLabel}: User {u.UserId} ({u.UserName}, L={u.ShiftManagerLevel})");
                }
            }
            var under = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
            _output.WriteLine($"Total UnderCapacity Violations: {under.Count}");
            foreach (var u in under) _output.WriteLine($"   {u}");

            var dups = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);
            _output.WriteLine($"Total Daily Duplicate Violations: {dups.Count}");
            foreach (var d in dups) _output.WriteLine($"   {d}");
        }

        CheckDates("1. Immediately After Optimize");

        ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
        ExactNightQuotaGuard.Enforce(solution, constraints);
        CheckDates("2. After NightQuota Force+Rebalance+Enforce");

        scheduler.PerformFinalManagerMixRepairSweep(solution);
        CheckDates("3. After PerformFinalManagerMixRepairSweep 1");

        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        CheckDates("4. After FillRemainingAfterForceApply");

        MorningEveningBalanceGuard.Enforce(solution, constraints);
        CheckDates("5. After MorningEveningBalanceGuard");

        OvertimeBalanceGuard.Enforce(solution, constraints);
        CheckDates("6. After OvertimeBalanceGuard");

        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
        CheckDates("7. After AdjacentShiftRestGuard");

        MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
        CheckDates("8. After MaxConsecutiveWorkdayGuard 1");

        ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        CheckDates("9. After FillRemainingAfterForceApply 2");

        ExactNightQuotaGuard.Enforce(solution, constraints);
        CheckDates("10. After ExactNightQuotaGuard");

        scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
        CheckDates("11. After PerformFinalManagerMixRepairSweep 2");

        AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
        CheckDates("12. After AdjacentShiftRestGuard 2");

        MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
        CheckDates("13. After MaxConsecutiveWorkdayGuard 2");

        ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
        CheckDates("14. After ForceFillAllMissingCoverage");

        var dupsBefore = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);
        _output.WriteLine($"Daily Duplicate Violations before StripDuplicates ({dupsBefore.Count}):");
        foreach (var v in dupsBefore) _output.WriteLine($"   {v}");

        DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);
        CheckDates("15. After DailyDuplicateAssignmentGuard.StripDuplicates");

        ShiftEligibilityGuard.StripIneligibleAssignments(solution, constraints);
        CheckDates("16. After ShiftEligibilityGuard.StripIneligibleAssignments");
    }
}



