using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using Xunit.Abstractions;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class DiagnoseDept3LiveTest
{
    private readonly ITestOutputHelper _output;

    public DiagnoseDept3LiveTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
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

    public void ClampInfeasibleQuotas(ShiftConstraints constraints)
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
}
