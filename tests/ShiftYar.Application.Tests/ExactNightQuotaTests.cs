using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ExactNightQuotaTests
{
    [Fact]
    public void Optimize_RespectsMinimumNightAndHolidayNightQuotas()
    {
        var start = new DateTime(2026, 8, 1);
        var holidays = new HashSet<DateTime>
        {
            new(2026, 8, 7),
            new(2026, 8, 14),
            new(2026, 8, 21),
            new(2026, 8, 28)
        };

        var users = new List<UserConstraint>
        {
            MakeUser(1, exactNights: 4, exactHolidayNights: 2),
            MakeUser(2, exactNights: null, exactHolidayNights: null),
            MakeUser(3, exactNights: null, exactHolidayNights: null),
            MakeUser(4, exactNights: null, exactHolidayNights: null)
        };

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(27),
            HolidayDates = holidays,
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
                EnforceMaxConsecutiveShifts = false,
                EnforceSpecialtyCapacity = true,
                EnforceNightShiftMonthlyCap = false
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            SoftWeights = SoftRuleWeights.CreateDefault()
        };

        var parameters = new SimulatedAnnealingParameters
        {
            InitialTemperature = 400,
            FinalTemperature = 0.1,
            CoolingRate = 0.93,
            MaxIterations = 3500,
            MaxIterationsWithoutImprovement = 400,
            PenaltyWeight = 1000
        };

        var solution = new SimulatedAnnealingScheduler(constraints, parameters).Optimize();
        var nights = solution.GetUserAllAssignments(1)
            .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
            .ToList();
        var holidayNights = nights.Count(a =>
            HolidayWeekendNightRules.IsHolidayWeekendNight(a.Date, holidays));
        Assert.True(nights.Count >= 4);
        Assert.True(holidayNights >= 2);

        var ordered = nights.OrderBy(a => a.Date).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            Assert.True(
                Math.Abs((ordered[i].Date.Date - ordered[i - 1].Date.Date).Days) > 2,
                "Night shifts must be spaced at least 2 days apart");
        }
    }

    [Fact]
    public void ExactNightQuotaGuard_FillsMissingMinimumNights()
    {
        var start = new DateTime(2026, 8, 1);
        var holidays = new HashSet<DateTime> { new(2026, 8, 7), new(2026, 8, 14) };
        var user = MakeUser(1, exactNights: 3, exactHolidayNights: 1);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(20),
            HolidayDates = holidays,
            UserConstraints = [user, MakeUser(2, null, null), MakeUser(3, null, null)],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 3, start.AddDays(2), ShiftLabel.Night, false);

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var nights = solution.GetUserAllAssignments(1).Where(a => a.ShiftLabel == ShiftLabel.Night).ToList();
        Assert.True(nights.Count >= 3);
        Assert.True(nights.Count(a => HolidayWeekendNightRules.IsHolidayWeekendNight(a.Date, holidays)) >= 1);
    }

    [Fact]
    public void PickSpreadDates_SpreadsAcrossCandidateRange()
    {
        var start = new DateTime(2026, 7, 23);
        var candidates = Enumerable.Range(0, 31).Select(i => start.AddDays(i)).ToList();
        var picks = ExactNightQuotaGuard.PickSpreadDates(candidates, Array.Empty<DateTime>(), needed: 4, minGapDays: 2);

        Assert.Equal(4, picks.Count);
        Assert.True(picks.Min() <= start.AddDays(5));
        Assert.True(picks.Max() >= start.AddDays(24));

        var ordered = picks.OrderBy(d => d).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            Assert.True((ordered[i] - ordered[i - 1]).Days > 2);
        }
    }

    [Fact]
    public void ExactNightQuotaGuard_ImprovesClusteredNightsTowardMonthSpread()
    {
        var start = new DateTime(2026, 7, 23);
        var end = new DateTime(2026, 8, 22);
        var holidays = new HashSet<DateTime>
        {
            new(2026, 7, 24), new(2026, 7, 31), new(2026, 8, 7), new(2026, 8, 14), new(2026, 8, 21)
        };

        var quotaUser = MakeUser(1, exactNights: 4, exactHolidayNights: 2);
        var others = new[] { 2, 3, 4, 5 }.Select(id => MakeUser(id, null, null)).ToList();
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            HolidayDates = holidays,
            UserConstraints = [quotaUser, .. others],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        // شب‌های تجمعی در ابتدای ماه (مشابه خروجی قبلی)
        solution.AddAssignment(1, 3, new DateTime(2026, 7, 24), ShiftLabel.Night, false);
        solution.AddAssignment(1, 3, new DateTime(2026, 7, 27), ShiftLabel.Night, false);
        solution.AddAssignment(1, 3, new DateTime(2026, 7, 31), ShiftLabel.Night, false);
        solution.AddAssignment(1, 3, new DateTime(2026, 8, 3), ShiftLabel.Night, false);

        // بقیه شب‌های ماه را با دیگران پر کن تا ظرفیت برای جابه‌جایی/swap موجود باشد
        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            if (solution.GetShiftAssignments(3, day).Any())
            {
                continue;
            }

            var uid = 2 + (day.Day % 4);
            solution.AddAssignment(uid, 3, day, ShiftLabel.Night, false);
        }

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var nights = solution.GetUserAllAssignments(1)
            .Where(a => a.ShiftLabel == ShiftLabel.Night)
            .OrderBy(a => a.Date)
            .ToList();

        Assert.True(nights.Count >= 4);
        Assert.True(nights.Count(a => HolidayWeekendNightRules.IsHolidayWeekendNight(a.Date, holidays)) >= 2);
        Assert.True(
            nights.Max(a => a.Date) >= new DateTime(2026, 8, 10),
            $"Expected nights spread into later August, got max={nights.Max(a => a.Date):yyyy-MM-dd}");
    }

    [Fact]
    public void ExactNightQuotaGuard_CanTakeNightFromUserAboveMinimum()
    {
        var start = new DateTime(2026, 8, 1);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(6),
            UserConstraints =
            [
                MakeUser(1, exactNights: 2, exactHolidayNights: 0),
                MakeUser(2, exactNights: 1, exactHolidayNights: 0),
                MakeUser(3, exactNights: null, exactHolidayNights: null)
            ],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 3, start.AddDays(0), ShiftLabel.Night, false);
        solution.AddAssignment(2, 3, start.AddDays(2), ShiftLabel.Night, false);
        solution.AddAssignment(2, 3, start.AddDays(5), ShiftLabel.Night, false);

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var user1Nights = solution.GetUserAllAssignments(1).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        var user2Nights = solution.GetUserAllAssignments(2).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);

        Assert.True(user1Nights >= 2);
        Assert.True(user2Nights >= 1);
    }

    [Fact]
    public void ExactNightQuotaGuard_UsesThursdayWhenFridayNightIsFull()
    {
        var start = new DateTime(2026, 7, 20);
        var friday = new DateTime(2026, 7, 24);
        var thursday = new DateTime(2026, 7, 23);
        var holidays = new HashSet<DateTime> { friday };
        var quotaUser = MakeUser(1, exactNights: 1, exactHolidayNights: 1);
        var other = MakeUser(2, null, null);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(7),
            HolidayDates = holidays,
            UserConstraints = [quotaUser, other, MakeUser(3, null, null)],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        // ظرفیت جمعه پر است → سهمیه باید روی پنجشنبه (شب قبل تعطیل) برود
        solution.AddAssignment(2, 3, friday, ShiftLabel.Night, false);

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var nights = solution.GetUserAllAssignments(1)
            .Where(a => a.ShiftLabel == ShiftLabel.Night)
            .Select(a => a.Date.Date)
            .ToList();

        Assert.Single(nights);
        Assert.Equal(thursday, nights[0]);
        Assert.True(HolidayWeekendNightRules.IsHolidayWeekendNight(nights[0], holidays));
    }

    private static UserConstraint MakeUser(int id, int? exactNights, int? exactHolidayNights) => new()
    {
        UserId = id,
        Gender = id % 2 == 0 ? UserGender.Female : UserGender.Male,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        ExactNightShiftCount = exactNights,
        ExactHolidayWeekendNightShiftCount = exactHolidayNights,
        MaxNightShiftsPerMonth = exactNights ?? 8,
        MinDaysBetweenNightShifts = 2,
        MaxConsecutiveShifts = 30,
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 7
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
