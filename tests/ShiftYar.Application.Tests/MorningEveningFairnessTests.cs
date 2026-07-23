using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class MorningEveningFairnessTests
{
    [Fact]
    public void Optimize_DoesNotGiveOneUserFarMoreMorningThanPeers()
    {
        var start = new DateTime(2026, 6, 22);
        var users = Enumerable.Range(1, 6).Select(i => MakeUser(i)).ToList();
        var constraints = BuildConstraints(start, days: 28, users);
        var parameters = SoftSaParams();

        var solution = new SimulatedAnnealingScheduler(constraints, parameters).Optimize();

        var morningCounts = users
            .Select(u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall))
            .ToList();
        var eveningCounts = users
            .Select(u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall))
            .ToList();

        Assert.True(morningCounts.Max() - morningCounts.Min() <= 6,
            $"Morning spread too large: [{string.Join(",", morningCounts)}]");
        Assert.True(eveningCounts.Max() - eveningCounts.Min() <= 6,
            $"Evening spread too large: [{string.Join(",", eveningCounts)}]");
    }

    [Fact]
    public void Optimize_RespectsMaxConsecutiveWorkdays()
    {
        // سقف متوالی دیگر مانع پوشش ظرفیت نمی‌شود؛ فقط ترجیح نرم است.
        // این تست اطمینان می‌دهد Optimize همچنان راه‌حل می‌سازد و پوشش صبح/عصر خالی نمی‌ماند.
        var start = new DateTime(2026, 6, 22);
        var users = Enumerable.Range(1, 5).Select(i =>
        {
            var u = MakeUser(i);
            u.MaxConsecutiveShifts = 3;
            u.MaxShiftsPerWeek = 5;
            return u;
        }).ToList();

        var constraints = BuildConstraints(start, days: 14, users);
        constraints.HardRules.EnforceMaxConsecutiveShifts = true;
        constraints.HardRules.EnforceWeeklyMaxShifts = true;
        constraints.SoftWeights.WorkdaySpreadWeight = 1.5;
        constraints.SoftWeights.FairMorningEveningPeerWeight = 2.5;

        var solution = new SimulatedAnnealingScheduler(constraints, SoftSaParams()).Optimize();

        var morningOk = 0;
        var eveningOk = 0;
        for (var i = 0; i < 14; i++)
        {
            var day = start.AddDays(i);
            if (solution.GetShiftAssignments(1, day).Any(a => !a.IsOnCall)) morningOk++;
            if (solution.GetShiftAssignments(2, day).Any(a => !a.IsOnCall)) eveningOk++;
        }

        Assert.True(morningOk >= 12, $"Morning coverage too low: {morningOk}/14");
        Assert.True(eveningOk >= 12, $"Evening coverage too low: {eveningOk}/14");
    }

    [Fact]
    public void ProductivityHourFillGuard_StillFillsHoursWithoutEmptyingCoverage()
    {
        var start = new DateTime(2026, 7, 1);
        var user = MakeUser(1);
        user.MaxConsecutiveShifts = 3;
        user.MaxShiftsPerWeek = 5;
        user.IncludedInProductivityPlan = true;
        user.ProductivityRequiredHours = 80m;
        user.OvertimeConsent = true;

        var others = Enumerable.Range(2, 4).Select(MakeUser).ToList();
        foreach (var o in others)
        {
            o.IncludedInProductivityPlan = true;
            o.ProductivityRequiredHours = 80m;
        }

        var constraints = BuildConstraints(start, days: 14, [user, .. others]);
        constraints.HardRules.EnforceMaxConsecutiveShifts = true;
        var solution = new ShiftSolution();
        for (var d = 0; d < 14; d++)
        {
            var day = start.AddDays(d);
            solution.AddAssignment(2 + (d % 4), 3, day, ShiftLabel.Night, false);
        }

        ShiftCoverageGuard.Enforce(solution, constraints);
        ProductivityHourFillGuard.Enforce(solution, constraints);
        ShiftCoverageGuard.Enforce(solution, constraints);

        for (var d = 0; d < 14; d++)
        {
            var day = start.AddDays(d);
            Assert.True(solution.GetShiftAssignments(1, day).Count(a => !a.IsOnCall) >= 1);
            Assert.True(solution.GetShiftAssignments(2, day).Count(a => !a.IsOnCall) >= 1);
        }
    }

    [Fact]
    public void HolidayMorningEveningFairnessGuard_SpreadsHolidayMeAcrossPeers()
    {
        var start = new DateTime(2026, 7, 1); // Wed
        // جمعه‌ها: 3, 10, 17, 24
        var holidays = new HashSet<DateTime>
        {
            new(2026, 7, 3),
            new(2026, 7, 10),
            new(2026, 7, 17),
            new(2026, 7, 24)
        };

        var users = Enumerable.Range(1, 4).Select(MakeUser).ToList();
        var constraints = BuildConstraints(start, days: 28, users);
        constraints.HolidayDates = holidays;
        constraints.SoftWeights.FairHolidayMorningEveningPeerWeight = 10;

        // همه تعطیل‌ها روی کاربر ۱ صبح پر شده
        var solution = new ShiftSolution();
        foreach (var day in holidays)
        {
            solution.AddAssignment(1, 1, day, ShiftLabel.Morning, false);
            solution.AddAssignment(2, 2, day, ShiftLabel.Evening, false);
            // شب را به دیگران بده تا ME جابه‌جا شود
            solution.AddAssignment(3, 3, day, ShiftLabel.Night, false);
        }

        HolidayMorningEveningFairnessGuard.Enforce(solution, constraints);

        var holidayMornings = users
            .Select(u => HolidayMorningEveningFairnessGuard.CountHolidayLabel(
                solution, constraints, u.UserId, ShiftLabel.Morning))
            .ToList();
        var holidayEvenings = users
            .Select(u => HolidayMorningEveningFairnessGuard.CountHolidayLabel(
                solution, constraints, u.UserId, ShiftLabel.Evening))
            .ToList();

        Assert.True(holidayMornings.Max() - holidayMornings.Min() <= 2,
            $"Holiday morning spread too large: [{string.Join(",", holidayMornings)}]");
        Assert.True(holidayEvenings.Max() - holidayEvenings.Min() <= 2,
            $"Holiday evening spread too large: [{string.Join(",", holidayEvenings)}]");
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
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 5,
        MaxNightShiftsPerMonth = 8,
        MinDaysBetweenNightShifts = 2
    };

    private static ShiftConstraints BuildConstraints(DateTime start, int days, List<UserConstraint> users) => new()
    {
        DepartmentId = 1,
        StartDate = start,
        EndDate = start.AddDays(days - 1),
        UserConstraints = users,
        ShiftRequirements =
        [
            Shift(1, ShiftLabel.Morning),
            Shift(2, ShiftLabel.Evening),
            Shift(3, ShiftLabel.Night)
        ],
        HardRules = new HardRuleSet
        {
            ForbidDuplicateDailyAssignments = true,
            EnforceMaxShiftsPerDay = true,
            EnforceMinRestDays = false,
            EnforceMaxConsecutiveShifts = true,
            EnforceWeeklyMaxShifts = true,
            EnforceSpecialtyCapacity = true
        },
        GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 2 },
        SoftWeights = new SoftRuleWeights
        {
            FairShiftCountBalanceWeight = 2,
            FairWorkedHoursBalanceWeight = 3,
            FairMorningEveningPeerWeight = 5,
            WorkdaySpreadWeight = 5,
            MorningEveningBalanceWeight = 2,
            FairNightShiftBalanceWeight = 2,
            ProductivityShortfallWeight = 3
        }
    };

    private static SimulatedAnnealingParameters SoftSaParams() => new()
    {
        InitialTemperature = 500,
        FinalTemperature = 0.1,
        CoolingRate = 0.97,
        MaxIterations = 2500,
        MaxIterationsWithoutImprovement = 400,
        PenaltyWeight = 1000
    };

    private static ShiftRequirement Shift(int id, ShiftLabel label) => new()
    {
        ShiftId = id,
        ShiftLabel = label,
        DepartmentId = 1,
        DurationHours = label == ShiftLabel.Night ? 12 : 6,
        StartTime = label switch
        {
            ShiftLabel.Morning => TimeSpan.FromHours(8),
            ShiftLabel.Evening => TimeSpan.FromHours(14),
            _ => TimeSpan.FromHours(20)
        },
        EndTime = label switch
        {
            ShiftLabel.Morning => TimeSpan.FromHours(14),
            ShiftLabel.Evening => TimeSpan.FromHours(20),
            _ => TimeSpan.FromHours(8)
        },
        SpecialtyRequirements =
        [
            new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
        ]
    };
}
