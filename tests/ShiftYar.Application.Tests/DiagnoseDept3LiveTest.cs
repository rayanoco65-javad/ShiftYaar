using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShiftYar.Application;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.Features.ShiftModel.Jobs;
using ShiftYar.Application.Features.ShiftModel.Services;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Application.Interfaces.ShiftModel;
using ShiftYar.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Application.Interfaces.Persistence;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class DiagnoseDept3LiveTest
{
    private readonly ITestOutputHelper _output;

    public DiagnoseDept3LiveTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Diagnostic offline test")]
    public void Diagnose_Dept3_OfflineFromScratch()
    {
        var scratchPath = @"C:\Users\Paria\.gemini\antigravity-ide\brain\5ea779c0-7392-4e4d-989c-e054dfe0206d\scratch\dept3_aban1405_constraints.json";
        var json = File.ReadAllText(scratchPath);
        var constraints = JsonSerializer.Deserialize<ShiftConstraints>(json)!;

        _output.WriteLine($"Global MaxShiftsPerDay: {constraints.GlobalConstraints.MaxShiftsPerDay}, EnforceMaxShiftsPerDay: {constraints.HardRules.EnforceMaxShiftsPerDay}");
        foreach (var u in constraints.UserConstraints)
        {
            _output.WriteLine($"User {u.UserId} ({u.UserName}): Permissions={u.AllowedShiftPermissions}, ShiftType={u.ShiftType}/{u.ShiftSubType}, ExactNight={u.ExactNightShiftCount}");
        }

        var u46 = constraints.UserConstraints.First(u => u.UserId == 46);
        _output.WriteLine("--- USER 46 CALENDAR DETAILS ---");
        for (var d = constraints.StartDate.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
        {
            var pDate = DateConverter.ConvertToPersianDate(d);
            var isUnavail = u46.UnavailableDates.Any(x => x.Date == d.Date);
            var unavailSlots = u46.UnavailableShiftSlots.Where(x => x.Date.Date == d.Date).Select(x => x.ShiftLabel.ToString()).ToList();
            var reqSlots = u46.RequiredShiftSlots.Where(x => x.Date.Date == d.Date).Select(x => x.ShiftLabel.ToString()).ToList();

            var status = new List<string>();
            if (isUnavail) status.Add("UNAVAILABLE_DAY");
            if (unavailSlots.Any()) status.Add("UNAVAIL_SLOT:" + string.Join(",", unavailSlots));
            if (reqSlots.Any()) status.Add("REQUIRED:" + string.Join(",", reqSlots));

            var nightShift = constraints.ShiftRequirements.First(s => s.ShiftLabel == ShiftLabel.Night);
            var dummySol = new ShiftSolution();
            ApprovedRequestGuard.ForceApply(dummySol, constraints);
            var canAccept = ExactNightQuotaGuard.CanAcceptNightAfterClearing(dummySol, constraints, u46, nightShift, d);

            _output.WriteLine($"  {pDate} ({d:yyyy-MM-dd}): {(status.Any() ? string.Join(" | ", status) : "FREE")} => CanNight={canAccept}");
        }

        // Apply feasibility clamping before scheduling
        ClampInfeasibleQuotas(constraints);

        ExactNightQuotaGuard.LogAction = msg => _output.WriteLine("[NightQuotaGuard] " + msg);

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1000,
            MaxIterationsWithoutImprovement = 200
        });

        var candidateSolution = scheduler.Optimize();
        void LogDay30(string step)
        {
            var mor = candidateSolution.GetShiftAssignments(7, new DateTime(2026, 11, 21)).Select(x => x.UserId).ToList();
            var eve = candidateSolution.GetShiftAssignments(8, new DateTime(2026, 11, 21)).Select(x => x.UserId).ToList();
            var nig = candidateSolution.GetShiftAssignments(9, new DateTime(2026, 11, 21)).Select(x => x.UserId).ToList();
            _output.WriteLine($"[{step}] Day 30 Mor({mor.Count}):[{string.Join(",", mor)}] | Eve({eve.Count}):[{string.Join(",", eve)}] | Nig({nig.Count}):[{string.Join(",", nig)}] | Total: {candidateSolution.Assignments.Count}");
        }
        void LogU41(string step)
        {
            var dates = candidateSolution.GetUserAllAssignments(41).Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).Select(a => a.Date.ToString("MM-dd")).ToList();
            _output.WriteLine($"[U41-{step}] Nights ({dates.Count}): [{string.Join(",", dates)}]");
        }
        LogDay30("Step 0 (Optimize)");
        LogU41("Step 0");
        var ineligAt0 = ShiftEligibilityGuard.GetViolations(candidateSolution, constraints);
        _output.WriteLine($"Ineligible at Step 0: {ineligAt0.Count}");
        foreach (var v in ineligAt0) _output.WriteLine("  INELIG_AT_0: " + v);
        ExactNightQuotaGuard.GlobalRebalanceNightQuotas(candidateSolution, constraints);
        ExactNightQuotaGuard.Enforce(candidateSolution, constraints);
        LogDay30("Step 1 (After NightQuota)");
        LogU41("Step 1");

        scheduler.PerformFinalManagerMixRepairSweep(candidateSolution);

        if (!scheduler.AreExactNightQuotasSatisfied(candidateSolution, out _))
        {
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(candidateSolution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(candidateSolution, constraints);
            ExactNightQuotaGuard.Enforce(candidateSolution, constraints);
        }

        ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);
        ShiftCoverageGuard.FillRemainingAfterForceApply(candidateSolution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);
        LogDay30("Step 2 (After FillRemaining)");
        LogU41("Step 2");
        MorningEveningBalanceGuard.Enforce(candidateSolution, constraints);
        LogDay30("Step 2.1 (After MorEveBalance)");
        LogU41("Step 2.1");
        OvertimeBalanceGuard.Enforce(candidateSolution, constraints);
        LogDay30("Step 2.2 (After OvertimeBalance)");
        LogU41("Step 2.2");
        AdjacentShiftRestGuard.StripForbiddenAdjacencies(candidateSolution, constraints);
        LogDay30("Step 2.3 (After AdjRest)");
        LogU41("Step 2.3");
        MaxConsecutiveWorkdayGuard.Enforce(candidateSolution, constraints);
        LogDay30("Step 2.4 (After MaxConsec)");
        LogU41("Step 2.4");
        ExactNightQuotaGuard.ForceSatisfyAllDeficits(candidateSolution, constraints);
        ExactNightQuotaGuard.Enforce(candidateSolution, constraints);
        LogDay30("Step 3 (Before 3-pass)");
        LogU41("Step 3");

        for (var pass = 0; pass < 3; pass++)
        {
            CoupledShiftRuleGuard.Enforce(candidateSolution, constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(candidateSolution, constraints);
            ShiftEligibilityGuard.StripIneligibleAssignments(candidateSolution, constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(candidateSolution, constraints);
            MaxConsecutiveWorkdayGuard.Enforce(candidateSolution, constraints);
            scheduler.PerformFinalManagerMixRepairSweep(candidateSolution, throwIfUnsatisfied: false);
            ShiftCoverageGuard.ForceFillAllMissingCoverage(candidateSolution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);

            LogDay30($"Step 4.Pass {pass}");
            LogU41($"Step 4.Pass {pass}");

            if (DailyDuplicateAssignmentGuard.GetViolations(candidateSolution, constraints).Count == 0
                && ShiftEligibilityGuard.GetViolations(candidateSolution, constraints).Count == 0
                && AdjacentShiftRestGuard.GetViolations(candidateSolution, constraints).Count == 0
                && CoupledShiftRuleGuard.GetViolations(candidateSolution, constraints).Count == 0
                && MaxConsecutiveWorkdayRules.GetViolations(candidateSolution, constraints).Count == 0
                && ShiftCoverageGuard.GetUnderCapacityViolations(candidateSolution, constraints).Count == 0)
            {
                break;
            }
        }

        if (ShiftCoverageGuard.GetUnderCapacityViolations(candidateSolution, constraints).Count > 0)
        {
            ShiftCoverageGuard.ForceFillAllMissingCoverage(candidateSolution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);
        }
        LogDay30("Step 5 (After Final ForceFill)");
        LogU41("Step 5");

        OvertimeBalanceGuard.Enforce(candidateSolution, constraints);
        LogDay30("Step 6 (After Overtime)");
        LogU41("Step 6");
        CoupledShiftRuleGuard.Enforce(candidateSolution, constraints);
        DailyDuplicateAssignmentGuard.StripDuplicates(candidateSolution, constraints);
        LogDay30("Step 7 (After StripDuplicates)");
        LogU41("Step 7");
        
        var ineligBefore = ShiftEligibilityGuard.GetViolations(candidateSolution, constraints);
        _output.WriteLine($"Ineligible before StripIneligible: {ineligBefore.Count}");
        foreach (var v in ineligBefore) _output.WriteLine("  INELIG: " + v);

        ShiftEligibilityGuard.StripIneligibleAssignments(candidateSolution, constraints);
        LogDay30("Step 8 (After StripIneligible)");
        LogU41("Step 8");
        ApprovedRequestGuard.ForceApply(candidateSolution, constraints);

        if (ShiftResponsibilityRules.IsApplicable(constraints))
        {
            ShiftResponsibilityRules.RepairResponsibilityDeficits(candidateSolution, constraints);
            ShiftResponsibilityRules.AssignResponsibilitiesToAllSlots(candidateSolution, constraints);
        }
        LogDay30("Step 9 (After Resp)");
        LogU41("Step 9");
        var satisfiedAt9 = scheduler.AreExactNightQuotasSatisfied(candidateSolution, out var unmetAt9);
        _output.WriteLine($"[After Step 9] AreExactNightQuotasSatisfied={satisfiedAt9}, unmet count={unmetAt9.Count}");
        foreach (var u in unmetAt9) _output.WriteLine("  UNMET_AT_9: " + u);

        scheduler.RefreshSolutionViolations(candidateSolution);

        if (!scheduler.AreExactNightQuotasSatisfied(candidateSolution, out _))
        {
            _output.WriteLine(">>> TRIGGERED LATE EXACT NIGHT QUOTA RECOVERY <<<");
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(candidateSolution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(candidateSolution, constraints);
            ExactNightQuotaGuard.Enforce(candidateSolution, constraints);
            ShiftCoverageGuard.ForceFillAllMissingCoverage(candidateSolution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);
            if (ShiftResponsibilityRules.IsApplicable(constraints))
            {
                ShiftResponsibilityRules.RepairResponsibilityDeficits(candidateSolution, constraints);
                ShiftResponsibilityRules.AssignResponsibilitiesToAllSlots(candidateSolution, constraints);
            }
            LogDay30("After Late Night Recovery");
        }

        var solution = candidateSolution;
        var u46Nights = solution.GetUserAllAssignments(46).Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).ToList();
        _output.WriteLine($"User 46 nights count after Pipeline: {u46Nights.Count}");

        var satisfies = scheduler.AreExactNightQuotasSatisfied(solution, out var unmet);
        _output.WriteLine($"AreExactNightQuotasSatisfied: {satisfies}");
        foreach (var m in unmet)
        {
            _output.WriteLine("  UNMET: " + m);
        }

        var coverageViolations = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        _output.WriteLine($"Coverage under-capacity violations: {coverageViolations.Count}");
        foreach (var c in coverageViolations)
        {
            _output.WriteLine("  COVERAGE: " + c);
        }

        var underViolations = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
        _output.WriteLine($"Under-capacity violations: {underViolations.Count}");
        foreach (var u in underViolations)
        {
            _output.WriteLine("  UNDER: " + u);
        }

        var respViolations = ShiftResponsibilityRules.IsApplicable(constraints)
            ? ShiftResponsibilityRules.GetViolations(solution, constraints)
            : new List<string>();
        _output.WriteLine($"Responsibility violations: {respViolations.Count}");
        foreach (var r in respViolations)
        {
            _output.WriteLine("  RESP: " + r);
        }

        var daily = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);
        var adjacency = AdjacentShiftRestGuard.GetViolations(solution, constraints);
        var eligibility = ShiftEligibilityGuard.GetViolations(solution, constraints);
        var coupled = CoupledShiftRuleGuard.GetViolations(solution, constraints);
        _output.WriteLine($"Hard Daily Violations: Daily={daily.Count}, Adjacency={adjacency.Count}, Eligibility={eligibility.Count}, Coupled={coupled.Count}");
        foreach (var a in adjacency) _output.WriteLine("  ADJ: " + a);
        foreach (var e in eligibility) _output.WriteLine("  ELIG: " + e);

        var testDates = new[] { new DateTime(2026, 10, 28), new DateTime(2026, 11, 01), new DateTime(2026, 11, 21) };
        foreach (var testD in testDates)
        {
            var pDate = DateConverter.ConvertToPersianDate(testD);
            _output.WriteLine($"\n--- DETAILED DIAGNOSTIC FOR {pDate} ({testD:yyyy-MM-dd}) ---");
            foreach (var req in constraints.ShiftRequirements)
            {
                var assigned = solution.GetShiftAssignments(req.ShiftId, testD).ToList();
                _output.WriteLine($"  Shift {req.ShiftLabel} (Id={req.ShiftId}): Assigned {assigned.Count} users: [{string.Join(", ", assigned.Select(x => x.UserId))}]");
            }
            foreach (var u in constraints.UserConstraints)
            {
                var userAsgs = solution.GetUserAssignments(u.UserId, testD).Select(x => x.ShiftLabel.ToString()).ToList();
                var unavail = u.UnavailableDates.Any(x => x.Date == testD.Date);
                var unavailSlots = u.UnavailableShiftSlots.Where(x => x.Date.Date == testD.Date).Select(x => x.ShiftLabel.ToString()).ToList();
                var reqSlots = u.RequiredShiftSlots.Where(x => x.Date.Date == testD.Date).Select(x => x.ShiftLabel.ToString()).ToList();
                var status = new List<string>();
                if (userAsgs.Any()) status.Add("ASSIGNED:" + string.Join(",", userAsgs));
                if (unavail) status.Add("LEAVE");
                if (unavailSlots.Any()) status.Add("UNAVAIL_SLOT:" + string.Join(",", unavailSlots));
                if (reqSlots.Any()) status.Add("REQUIRED:" + string.Join(",", reqSlots));

                var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay) : 2;
                var effectiveMax = Math.Max(maxPerDay, reqSlots.Count);
                var existing = solution.GetUserAssignments(u.UserId, testD).Select(a => a.ShiftLabel).ToList();
                var canEvening = ShiftEligibilityResolver.IsAssignmentAllowed(u, existing, ShiftLabel.Evening, effectiveMax, true, testD, constraints.StartDate);
                var canNight = ShiftEligibilityResolver.IsAssignmentAllowed(u, existing, ShiftLabel.Night, effectiveMax, true, testD, constraints.StartDate);
                var adjEvening = AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(u.UserId), testD, ShiftLabel.Evening, constraints);
                var adjNight = AdjacentShiftRestRules.WouldConflict(solution.GetUserAllAssignments(u.UserId), testD, ShiftLabel.Night, constraints);
                var maxConsec = MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(solution, constraints, u, testD);

                var canAcceptEve = ShiftCoverageGuard.CanAcceptShift(solution, constraints, u, testD, ShiftLabel.Evening);
                var mayTakeEve = ShiftEligibilityResolver.MayTakeLabelOnDate(u, ShiftLabel.Evening, testD);
                var canAcceptMor = ShiftCoverageGuard.CanAcceptShift(solution, constraints, u, testD, ShiftLabel.Morning);
                var mayTakeMor = ShiftEligibilityResolver.MayTakeLabelOnDate(u, ShiftLabel.Morning, testD);
                var isEligMor = typeof(ShiftCoverageGuard).GetMethod("IsEligibleForCoverageFill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, new object[] { solution, constraints, u, ShiftLabel.Morning, testD, false });
                _output.WriteLine($"    User {u.UserId} ({u.UserName}): [{(status.Any() ? string.Join(" | ", status) : "FREE")}] | CanMor={canAcceptMor}, MayTakeMor={mayTakeMor}, IsEligMor={isEligMor} | CanEve={canAcceptEve}");
            }
        }

        _output.WriteLine("\n=== USER MONTHLY WORK SCHEDULES ===");
        var allDates = Enumerable.Range(0, (constraints.EndDate.Date - constraints.StartDate.Date).Days + 1)
            .Select(i => constraints.StartDate.Date.AddDays(i)).ToList();
        foreach (var u in constraints.UserConstraints)
        {
            var daysStr = string.Join("", allDates.Select(d =>
            {
                var asgs = solution.GetUserAssignments(u.UserId, d).ToList();
                if (!asgs.Any()) return ".";
                if (asgs.Count > 1) return "D";
                return asgs[0].ShiftLabel switch { ShiftLabel.Morning => "M", ShiftLabel.Evening => "E", ShiftLabel.Night => "N", _ => "?" };
            }));
            _output.WriteLine($"User {u.UserId} ({u.UserName}, MaxConsec={u.MaxConsecutiveShifts}): {daysStr}");
        }

        Assert.Empty(coverageViolations);
        Assert.Empty(respViolations);
        Assert.True(satisfies);
        Assert.Empty(daily);
        Assert.Empty(adjacency);
        Assert.Empty(eligibility);
        Assert.Empty(coupled);
    }

    private void ClampInfeasibleQuotas(ShiftConstraints constraints)
    {
        foreach (var user in constraints.UserConstraints)
        {
            if (user.ExactNightShiftCount.HasValue)
            {
                var maxNights = CalculateMaxFeasibleNightShifts(user, constraints);
                _output.WriteLine($"[Clamp] User {user.UserId} ({user.UserName}): ExactNight={user.ExactNightShiftCount} => MaxFeasible={maxNights}");
                if (user.ExactNightShiftCount.Value > maxNights)
                {
                    _output.WriteLine($"  ===> CLAMPED user {user.UserId} from {user.ExactNightShiftCount.Value} to {maxNights}");
                    user.ExactNightShiftCount = maxNights;
                }
            }

            if (user.ExactHolidayWeekendNightShiftCount.HasValue &&
                user.ExactHolidayWeekendNightShiftCount.Value > (user.ExactNightShiftCount ?? 0))
            {
                user.ExactHolidayWeekendNightShiftCount = user.ExactNightShiftCount;
            }
        }
    }

    public static int CalculateMaxFeasibleNightShifts(UserConstraint user, ShiftConstraints constraints)
    {
        if (!ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night))
        {
            return 0;
        }

        var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay
            ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay)
            : 2;

        var start = constraints.StartDate.Date;
        var end = constraints.EndDate.Date;
        var daysCount = (end - start).Days + 1;
        var candidateDates = new List<DateTime>();

        for (var i = 0; i < daysCount; i++)
        {
            var d = start.AddDays(i);

            if (user.UnavailableDates.Any(x => x.Date == d))
            {
                continue;
            }

            if (user.UnavailableShiftSlots.Any(x => x.Date == d && x.ShiftLabel == ShiftLabel.Night))
            {
                continue;
            }

            var reqOnD = user.RequiredShiftSlots.Where(x => x.Date == d).ToList();
            if (reqOnD.Any(x => x.ShiftLabel == ShiftLabel.Night))
            {
                candidateDates.Add(d);
                continue;
            }

            if (reqOnD.Any(x => x.ShiftLabel == ShiftLabel.Evening))
            {
                continue;
            }

            if (reqOnD.Any(x => x.ShiftLabel == ShiftLabel.Morning))
            {
                if (maxPerDay < 2 || !ShiftEligibilityResolver.SupportsMorningNightCombo(user))
                {
                    continue;
                }
            }

            var nextD = d.AddDays(1);
            var reqOnNext = user.RequiredShiftSlots.Where(x => x.Date == nextD).ToList();
            if (reqOnNext.Any(x => x.ShiftLabel == ShiftLabel.Morning))
            {
                continue;
            }

            if (reqOnNext.Any(x => x.ShiftLabel == ShiftLabel.Evening))
            {
                var allowEvening = user.ResolveAllowEveningAfterNightShift(constraints.HardRules.AllowEveningAfterNightShift);
                if (!allowEvening)
                {
                    continue;
                }
            }

            if (reqOnNext.Any(x => x.ShiftLabel == ShiftLabel.Night))
            {
                var allowNight = user.ResolveAllowNightShiftAfterNightShift(constraints.HardRules.AllowNightShiftAfterNightShift);
                if (!allowNight)
                {
                    continue;
                }
            }

            var prevD = d.AddDays(-1);
            var reqOnPrev = user.RequiredShiftSlots.Where(x => x.Date == prevD).ToList();
            if (reqOnPrev.Any(x => x.ShiftLabel == ShiftLabel.Night))
            {
                var allowNight = user.ResolveAllowNightShiftAfterNightShift(constraints.HardRules.AllowNightShiftAfterNightShift);
                if (!allowNight)
                {
                    continue;
                }
            }

            candidateDates.Add(d);
        }

        var allowConsecutiveNight = user.ResolveAllowNightShiftAfterNightShift(constraints.HardRules.AllowNightShiftAfterNightShift);
        if (!allowConsecutiveNight && candidateDates.Count > 1)
        {
            var nonConsecutiveCount = 0;
            DateTime? lastAssigned = null;
            foreach (var cd in candidateDates)
            {
                if (!lastAssigned.HasValue || (cd - lastAssigned.Value).TotalDays > 1)
                {
                    nonConsecutiveCount++;
                    lastAssigned = cd;
                }
            }
            return nonConsecutiveCount;
        }

        return candidateDates.Count;
    }

    [Fact(Skip = "Diagnostic live DB inspection")]
    public async Task Inspect_Dept3_Requests_And_Validation_FromLiveDb()
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

        var userRepo = sp.GetRequiredService<IEfRepository<ShiftYar.Domain.Entities.UserModel.User>>();
        var reqRepo = sp.GetRequiredService<IEfRepository<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest>>();
        var deptRepo = sp.GetRequiredService<IEfRepository<ShiftYar.Domain.Entities.DepartmentModel.Department>>();
        var shiftRepo = sp.GetRequiredService<IEfRepository<ShiftYar.Domain.Entities.ShiftModel.Shift>>();
        var deptSettingsRepo = sp.GetRequiredService<IEfRepository<ShiftYar.Domain.Entities.DepartmentModel.DepartmentSchedulingSettings>>();

        var (users, _) = await userRepo.GetByFilterAsync(
            new SimpleFilter<ShiftYar.Domain.Entities.UserModel.User>(u => u.DepartmentId == 3 && (u.IsActive == null || u.IsActive == true)),
            "UserResponsibilities", "UserResponsibilities.DepartmentResponsibility");

        _output.WriteLine($"=== DEPARTMENT 3 USERS ({users.Count}) ===");
        foreach (var u in users)
        {
            _output.WriteLine($"User {u.Id} ({u.FullName}): AllowedShiftPermissions={u.AllowedShiftPermissions}, ShiftType={u.ShiftType}, ShiftSubType={u.ShiftSubType}");
        }

        var userIds = users.Select(u => u.Id!.Value).ToList();
        var (allRequests, _) = await reqRepo.GetByFilterAsync(
            new SimpleFilter<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest>(r =>
                r.UserId != null && userIds.Contains(r.UserId.Value)),
            "User", "User.UserResponsibilities", "User.UserResponsibilities.DepartmentResponsibility");

        _output.WriteLine($"\n=== DEPARTMENT 3 REQUESTS ({allRequests.Count}) ===");
        foreach (var r in allRequests.OrderBy(r => r.RequestDate))
        {
            _output.WriteLine($"Req {r.Id}: User {r.UserId} ({r.User?.FullName}), Date={r.RequestDate:yyyy-MM-dd} (P={DateConverter.ConvertToPersianDate(r.RequestDate ?? DateTime.MinValue)}), Action={r.RequestAction}, Type={r.RequestType}, Status={r.Status}, ApprovalDate={r.ApprovalDate:yyyy-MM-dd HH:mm:ss}");
        }

        var (shifts, _) = await shiftRepo.GetByFilterAsync(
            new SimpleFilter<ShiftYar.Domain.Entities.ShiftModel.Shift>(s => s.DepartmentId == 3),
            "RequiredSpecialties", "RequiredSpecialties.DepartmentResponsibility",
            "RequiredResponsibilities", "RequiredResponsibilities.DepartmentResponsibility");

        var dept = await deptRepo.GetByIdAsync(3, "Responsibilities");
        var deptSettings = (await deptSettingsRepo.GetByFilterAsync(new SimpleFilter<ShiftYar.Domain.Entities.DepartmentModel.DepartmentSchedulingSettings>(s => s.DepartmentId == 3))).Item1.FirstOrDefault();

        _output.WriteLine("\n=== DETAILED TRACE FOR DATE 2026-10-27 (1405/08/05) ===");
        var traceDate = new DateTime(2026, 10, 27);
        var tShifts = shifts.ToList();
        var tUsers = users.ToList();
        var tTodayDemand = ApprovedResponsibilityCapacityValidator.CalculateDailyShiftDemand(tShifts, false);
        var tYestNightDemand = ApprovedResponsibilityCapacityValidator.CalculateNightShiftDemand(tShifts, false);
        var (tM, tE, tN, _) = ApprovedResponsibilityCapacityValidator.CalculateShiftDemandsByLabelForDepartment(tShifts, false);
        var tMaxLong = ApprovedLeaveCapacityValidator.CalculateMaxLongShifts(tM, tE, tN, tUsers, deptSettings?.MaxShiftsPerDay ?? 1);
        var tMaxCap = ApprovedLeaveCapacityValidator.CalculateMaxDailyLeaveCapacity(tUsers.Count, tTodayDemand, tYestNightDemand, tMaxLong);

        _output.WriteLine($"TodayDemand={tTodayDemand}, YestNightDemand={tYestNightDemand}, M={tM}, E={tE}, N={tN}");
        _output.WriteLine($"TotalUsers={tUsers.Count}, MaxLong={tMaxLong}, MaxDailyLeaveCap={tMaxCap}");

        var tApprovedReqs = allRequests.Where(x =>
            x.Status == Domain.Enums.ShiftRequestModel.RequestStatus.Approved &&
            x.RequestAction == Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOffShift &&
            x.RequestType == Domain.Enums.ShiftRequestModel.RequestType.FullDay &&
            x.RequestDate?.Date == traceDate).ToList();

        _output.WriteLine($"Approved OFF requests on this date: {tApprovedReqs.Count} [UserIds: {string.Join(",", tApprovedReqs.Select(x => x.UserId))}]");
        var tApprovedUserIds = tApprovedReqs.Select(x => x.UserId!.Value).Distinct().ToHashSet();

        var tAvailUsers = tUsers.Where(u => !tApprovedUserIds.Contains(u.Id!.Value)).ToList();
        var tAvailLong = ApprovedLeaveCapacityValidator.CalculateMaxLongShifts(tM, tE, tN, tAvailUsers, deptSettings?.MaxShiftsPerDay ?? 1);
        _output.WriteLine($"AvailUsers={tAvailUsers.Count}, AvailLong={tAvailLong}, MaxCoverable={tAvailUsers.Count + tAvailLong}, TodayDemand={tTodayDemand}");

        _output.WriteLine("\n=== SIMULATING LEAVE CAPACITY VALIDATION FOR EACH OFF REQUEST ===");
        foreach (var r in allRequests.Where(r => r.RequestAction == Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOffShift && r.RequestType == Domain.Enums.ShiftRequestModel.RequestType.FullDay && r.RequestDate.HasValue).OrderBy(r => r.RequestDate))
        {
            var reqDate = r.RequestDate.Value.Date;
            var isHolToday = false;
            var isHolYest = false;

            var otherApproved = allRequests.Where(x =>
                x.Id != r.Id &&
                x.Status == Domain.Enums.ShiftRequestModel.RequestStatus.Approved &&
                x.RequestAction == Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOffShift &&
                x.RequestType == Domain.Enums.ShiftRequestModel.RequestType.FullDay &&
                x.RequestDate?.Date == reqDate).ToList();

            var err = ApprovedResponsibilityCapacityValidator.ValidateDailyLeaveCapacity(
                shifts,
                reqDate,
                isHolToday,
                isHolYest,
                r.User,
                dept?.Responsibilities ?? new List<DepartmentResponsibility>(),
                otherApproved,
                users,
                deptSettings?.MaxShiftsPerDay ?? 1);

            _output.WriteLine($"Req {r.Id} (User {r.UserId}, Date={DateConverter.ConvertToPersianDate(reqDate)}, Status={r.Status}): ValidationResult = {(err == null ? "ALLOWED (NULL)" : "BLOCKED: " + err)}");
        }
    }

    [Fact(Skip = "Diagnostic live DB test")]
    public async Task Diagnose_Dept3_FromLiveDb()
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
        var service = (ShiftSchedulingService)sp.GetRequiredService<IShiftSchedulingService>();

        var req = new ShiftSchedulingRequestDto
        {
            DepartmentId = 3,
            StartDate = "1405/08/01",
            EndDate = "1405/08/30",
            Algorithm = SchedulingAlgorithm.SimulatedAnnealing
        };

        _output.WriteLine("Calling LoadConstraintsAsync for Dept 3 Aban 1405...");
        var constraints = await service.LoadConstraintsAsync(req);
        _output.WriteLine($"Loaded {constraints.UserConstraints.Count} users, {constraints.ShiftRequirements.Count} shift requirements.");
        _output.WriteLine($"MaxShiftsPerDay: {constraints.GlobalConstraints.MaxShiftsPerDay}, EnforceMaxShiftsPerDay: {constraints.HardRules.EnforceMaxShiftsPerDay}");



        var jsonPath = @"C:\Users\Paria\.gemini\antigravity-ide\brain\5b66b4b0-3bf2-45f7-9137-61a0a117d18a\scratch\dept3_live_constraints.json";
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(constraints, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine("Saved live constraints to: " + jsonPath);

        // Test hypothesis: MaxConsecutiveShifts is the root cause
        // constraints.HardRules.EnforceMaxConsecutiveShifts = false;
        foreach (var u in constraints.UserConstraints)
        {
            u.MaxConsecutiveShifts = 6; // allow working Saturday through Thursday
        }

        var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters
        {
            MaxIterations = 1000,
            MaxIterationsWithoutImprovement = 200
        });

        var candidateSolution = scheduler.Optimize();
        var initialUnderCapacity = ShiftCoverageGuard.GetUnderCapacityViolations(candidateSolution, constraints);
        var initialMaxConsec = MaxConsecutiveWorkdayRules.GetViolations(candidateSolution, constraints);
        _output.WriteLine($"[AFTER OPTIMIZE] UnderCapacity: {initialUnderCapacity.Count}, MaxConsecViolations: {initialMaxConsec.Count}");
        foreach (var v in initialUnderCapacity) _output.WriteLine("  InitUnderCap: " + v);
        foreach (var v in initialMaxConsec) _output.WriteLine("  InitMaxConsec: " + v);

        ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);
        ShiftCoverageGuard.FillRemainingAfterForceApply(candidateSolution, constraints);
        ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);

        for (var pass = 0; pass < 3; pass++)
        {
            CoupledShiftRuleGuard.Enforce(candidateSolution, constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(candidateSolution, constraints);
            ShiftEligibilityGuard.StripIneligibleAssignments(candidateSolution, constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(candidateSolution, constraints);
            MaxConsecutiveWorkdayGuard.Enforce(candidateSolution, constraints);
            scheduler.PerformFinalManagerMixRepairSweep(candidateSolution, throwIfUnsatisfied: false);
            ShiftCoverageGuard.ForceFillAllMissingCoverage(candidateSolution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);
        }

        if (ShiftCoverageGuard.GetUnderCapacityViolations(candidateSolution, constraints).Count > 0)
        {
            ShiftCoverageGuard.ForceFillAllMissingCoverage(candidateSolution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(candidateSolution, constraints);
        }

        var underCapacity = ShiftCoverageGuard.GetUnderCapacityViolations(candidateSolution, constraints);
        _output.WriteLine($"=== UNDER CAPACITY VIOLATIONS COUNT: {underCapacity.Count} ===");
        foreach (var v in underCapacity)
        {
            _output.WriteLine("  VIOLATION: " + v);
        }

        var violationDates = new[]
        {
            new DateTime(2026, 10, 25), // 08/03
            new DateTime(2026, 10, 27), // 08/05
            new DateTime(2026, 10, 28), // 08/06
            new DateTime(2026, 10, 29), // 08/07
            new DateTime(2026, 11, 01), // 08/10
            new DateTime(2026, 11, 08), // 08/17
            new DateTime(2026, 11, 15), // 08/24
        };

        foreach (var d in violationDates)
        {
            var pDate = DateConverter.ConvertToPersianDate(d);
            _output.WriteLine($"\n--- DETAILED INSPECTION FOR {pDate} ({d:yyyy-MM-dd}) ---");
            foreach (var sReq in constraints.ShiftRequirements)
            {
                var assigned = candidateSolution.GetShiftAssignments(sReq.ShiftId, d).Select(x => x.UserId).ToList();
                _output.WriteLine($"  Shift {sReq.ShiftLabel} (Id={sReq.ShiftId}): Count={assigned.Count} [{string.Join(",", assigned)}]");
            }

            foreach (var u in constraints.UserConstraints)
            {
                var userAsgs = candidateSolution.GetUserAssignments(u.UserId, d).Select(x => x.ShiftLabel.ToString()).ToList();
                var unavail = u.UnavailableDates.Any(x => x.Date == d.Date);
                var unavailSlots = u.UnavailableShiftSlots.Where(x => x.Date.Date == d.Date).Select(x => x.ShiftLabel.ToString()).ToList();
                var reqSlots = u.RequiredShiftSlots.Where(x => x.Date.Date == d.Date).Select(x => x.ShiftLabel.ToString()).ToList();
                var existing = candidateSolution.GetUserAssignments(u.UserId, d).Select(a => a.ShiftLabel).ToList();

                var canAcceptEve = ShiftCoverageGuard.CanAcceptShift(candidateSolution, constraints, u, d, ShiftLabel.Evening);
                var canAcceptMor = ShiftCoverageGuard.CanAcceptShift(candidateSolution, constraints, u, d, ShiftLabel.Morning);

                var isEligEve = typeof(ShiftCoverageGuard).GetMethod("IsEligibleForCoverageFill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, new object[] { candidateSolution, constraints, u, ShiftLabel.Evening, d, false });
                var isEligMor = typeof(ShiftCoverageGuard).GetMethod("IsEligibleForCoverageFill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, new object[] { candidateSolution, constraints, u, ShiftLabel.Morning, d, false });

                // Check reasons for Eve
                var maxPerDay = constraints.HardRules.EnforceMaxShiftsPerDay ? Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay) : 2;
                var effectiveMax = Math.Max(maxPerDay, reqSlots.Count);
                var trialEve = existing.Append(ShiftLabel.Evening).Distinct().ToList();
                if (u.CanBypassMaxShiftsPerDay(trialEve, d) && effectiveMax < 2) effectiveMax = 2;

                var isAllowedEve = ShiftEligibilityResolver.IsAssignmentAllowed(u, existing, ShiftLabel.Evening, effectiveMax, true, d);
                var dailyAddEve = DailyAssignmentRules.CanAddShift(existing, ShiftLabel.Evening, effectiveMax, true);
                var adjEve = AdjacentShiftRestRules.WouldConflict(candidateSolution.GetUserAllAssignments(u.UserId), d, ShiftLabel.Evening, constraints);
                var maxConsecEve = MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(candidateSolution, constraints, u, d);

                _output.WriteLine($"    User {u.UserId} ({u.UserName}): Asg=[{string.Join(",", userAsgs)}] Leave={unavail} Perms={u.AllowedShiftPermissions} | Eve: CanAcc={canAcceptEve}, Elig={isEligEve}, Allowed={isAllowedEve}, DailyAdd={dailyAddEve}, AdjConf={adjEve}, MaxConsec={maxConsecEve}");
            }
        }

        var hardDaily = DailyDuplicateAssignmentGuard.GetViolations(candidateSolution, constraints);
        var eligibility = ShiftEligibilityGuard.GetViolations(candidateSolution, constraints);
        var adj = AdjacentShiftRestGuard.GetViolations(candidateSolution, constraints);
        _output.WriteLine($"Hard Daily: {hardDaily.Count}, Eligibility: {eligibility.Count}, Adjacency: {adj.Count}");

        Assert.Empty(underCapacity);
    }
}
