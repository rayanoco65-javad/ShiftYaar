using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class FixedShiftDailyPresenceTests
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
    public void FixedMorningUser_WorksEveryNonHolidayDay_DespiteRestRules()
    {
        var start = new DateTime(2026, 8, 23);
        var days = 7;
        var holiday = start.AddDays(5); // جمعه فرضی

        var fixedUser = new UserConstraint
        {
            UserId = 10,
            Gender = UserGender.Female,
            SpecialtyId = 10,
            IsActive = true,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftLabels = [ShiftLabel.Morning],
            // مقادیر سخت‌گیرانه که بدون استثنای فیکس، حضور روزانه را ناممکن می‌کرد
            MaxConsecutiveShifts = 3,
            MinRestDaysBetweenShifts = 1,
            MaxShiftsPerWeek = 5
        };

        var rotating1 = RotatingUser(11, UserGender.Male);
        var rotating2 = RotatingUser(12, UserGender.Female);
        var rotating3 = RotatingUser(13, UserGender.Male);

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            HolidayDates = [holiday.Date],
            UserConstraints = [fixedUser, rotating1, rotating2, rotating3],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning, requiredTotal: 2),
                Shift(2, ShiftLabel.Evening, requiredTotal: 1),
                Shift(3, ShiftLabel.Night, requiredTotal: 1)
            ],
            HardRules = new HardRuleSet
            {
                ForbidDuplicateDailyAssignments = true,
                EnforceMaxShiftsPerDay = true,
                EnforceMinRestDays = true,
                EnforceMaxConsecutiveShifts = true,
                EnforceWeeklyMaxShifts = true,
                EnforceSpecialtyCapacity = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 }
        };

        // شبیه‌سازی LoadConstraints: پرسنل فیکس در همه روزهای غیرتعطیل باید شیفت خودش باشد
        for (var d = start.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
        {
            if (constraints.HolidayDates.Contains(d))
            {
                continue;
            }

            fixedUser.RequiredShiftSlots.Add(new ShiftSlotConstraint
            {
                Date = d,
                ShiftLabel = ShiftLabel.Morning
            });
        }

        for (var run = 0; run < 5; run++)
        {
            var solution = new SimulatedAnnealingScheduler(constraints, FastParameters).Optimize();

            for (var d = start.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
            {
                var dayAssignments = solution.GetUserAssignments(fixedUser.UserId, d).ToList();
                if (constraints.HolidayDates.Contains(d))
                {
                    continue; // در تعطیلات الزامی نیست
                }

                Assert.True(
                    dayAssignments.Any(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall),
                    $"Run {run}: fixed-morning user missing on {d:yyyy-MM-dd}");
            }

            // هیچ انتساب غیر صبح نباید داشته باشد
            Assert.All(
                solution.GetUserAllAssignments(fixedUser.UserId),
                a => Assert.Equal(ShiftLabel.Morning, a.ShiftLabel));

            Assert.Empty(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
        }
    }

    private static UserConstraint RotatingUser(int id, UserGender gender) => new()
    {
        UserId = id,
        Gender = gender,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        MaxConsecutiveShifts = 7,
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 7
    };

    private static ShiftRequirement Shift(int id, ShiftLabel label, int requiredTotal) => new()
    {
        ShiftId = id,
        ShiftLabel = label,
        DepartmentId = 1,
        DurationHours = 8,
        SpecialtyRequirements =
        [
            new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = requiredTotal }
        ]
    };
}
