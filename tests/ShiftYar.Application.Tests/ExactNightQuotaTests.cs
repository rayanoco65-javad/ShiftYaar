using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ExactNightQuotaTests
{
    [Fact]
    public void Optimize_RespectsExactNightAndHolidayNightQuotas()
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
        var holidayNights = nights.Count(a => holidays.Contains(a.Date.Date));

        Assert.Equal(4, nights.Count);
        Assert.Equal(2, holidayNights);

        var ordered = nights.OrderBy(a => a.Date).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            Assert.True(
                Math.Abs((ordered[i].Date.Date - ordered[i - 1].Date.Date).Days) > 1,
                "Night shifts must be spaced apart");
        }
    }

    [Fact]
    public void ExactNightQuotaGuard_FillsMissingNights()
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
        Assert.Equal(3, nights.Count);
        Assert.Equal(1, nights.Count(a => holidays.Contains(a.Date.Date)));
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
        MinDaysBetweenNightShifts = 1,
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
