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
    public void ExactNightQuotaGuard_ClaimsHolidayNightWhenAllDaysAreFull()
    {
        var start = new DateTime(2026, 6, 22);
        var end = new DateTime(2026, 7, 5);
        // جمعه‌ها تعطیل → پنجشنبه و جمعه شبِ تعطیل/آخرهفته‌اند
        var holidays = new HashSet<DateTime>
        {
            new(2026, 6, 26),
            new(2026, 7, 3)
        };

        var needy = MakeUser(11, exactNights: 1, exactHolidayNights: 1);
        var rich = MakeUser(4, exactNights: 2, exactHolidayNights: 1);
        var filler = MakeUser(5, exactNights: null, exactHolidayNights: null);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            HolidayDates = holidays,
            UserConstraints = [needy, rich, filler],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        // همه روزها پر؛ needy هیچ شبی ندارد
        var days = Enumerable.Range(0, (end - start).Days + 1).Select(i => start.AddDays(i)).ToList();
        foreach (var day in days)
        {
            var uid = constraints.IsHolidayWeekendNight(day) ? 4 : 5;
            solution.AddAssignment(uid, 3, day, ShiftLabel.Night, false);
        }

        // rich بالای حداقل است (چند شب تعطیل + عادی)
        Assert.True(solution.GetUserAllAssignments(4).Count(a => a.ShiftLabel == ShiftLabel.Night) > 2);

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var nights = solution.GetUserAllAssignments(11)
            .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
            .ToList();
        Assert.True(nights.Count >= 1, "User 11 must receive at least 1 night via claim-from-donor");
        Assert.True(
            nights.Count(a => HolidayWeekendNightRules.IsHolidayWeekendNight(a.Date, holidays)) >= 1,
            "User 11 must receive a holiday/weekend night");

        var richNights = solution.GetUserAllAssignments(4).Count(a => a.ShiftLabel == ShiftLabel.Night);
        Assert.True(richNights >= 2, "Donor must stay at/above own minimum total nights");
    }

    [Fact]
    public void ExactNightQuotaGuard_DoesNotLeaveExactNightOneAtZeroWhenDonorHasSurplus()
    {
        var start = new DateTime(2026, 7, 1);
        var friday = new DateTime(2026, 7, 3);
        var holidays = new HashSet<DateTime> { friday };
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(6),
            HolidayDates = holidays,
            UserConstraints =
            [
                MakeUser(11, exactNights: 1, exactHolidayNights: 1),
                MakeUser(4, exactNights: 2, exactHolidayNights: 1),
                MakeUser(5, exactNights: null, exactHolidayNights: null)
            ],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        // ظرفیت هر روز ۱ نفر — همه پر؛ کاربر ۱۱ صفر شب
        // rich چند شب تعطیل + عادی دارد تا بتواند یکی را اهدا کند و هنوز ≥ حداقل بماند
        foreach (var day in Enumerable.Range(0, 7).Select(i => start.AddDays(i)))
        {
            var isHolidayNight = HolidayWeekendNightRules.IsHolidayWeekendNight(day, holidays);
            var uid = isHolidayNight || day.Day % 2 == 0 ? 4 : 5;
            solution.AddAssignment(uid, 3, day, ShiftLabel.Night, false);
        }

        Assert.True(solution.GetUserAllAssignments(4).Count(a => a.ShiftLabel == ShiftLabel.Night) >= 3);

        // کاربر ۱۱ صبح همان روز تعطیل را دارد؛ صبح+شب مجاز است و صبح نباید پاک شود
        solution.AddAssignment(11, 1, friday, ShiftLabel.Morning, false);

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var u11 = solution.GetUserAllAssignments(11).Where(a => a.ShiftLabel == ShiftLabel.Night).ToList();
        Assert.True(u11.Count >= 1, $"Expected >=1 night for user 11, got {u11.Count}");
        Assert.Contains(u11, a => HolidayWeekendNightRules.IsHolidayWeekendNight(a.Date, holidays));

        // صبح+شب همان روز جمعه مجاز است؛ اگر شب پنجشنبه گرفته شود صبح جمعه باید پاک شود (توالی ممنوع)
        var fridayMorning = solution.GetUserAllAssignments(11)
            .Any(a => a.Date.Date == friday && a.ShiftLabel == ShiftLabel.Morning);
        var nightOnFriday = u11.Any(a => a.Date.Date == friday);
        if (nightOnFriday)
        {
            Assert.True(fridayMorning, "Friday morning+night same day must remain");
        }

        Assert.True(
            solution.GetUserAllAssignments(4).Count(a => a.ShiftLabel == ShiftLabel.Night) >= 2,
            "Donor must remain at/above minimum");
    }

    [Fact]
    public void ExactNightQuotaGuard_SwapsWeekdayForHoliday_WhenDonorAtExactNightFloorWithHolidaySurplus()
    {
        // سناریوی واقعی: کاربر ۱۱ یک شب عادی دارد و ExactHoliday=1؛
        // اهداکننده روی کف ExactNight است ولی مازاد شب تعطیل دارد → تعویض، نه دزدی خالص.
        var start = new DateTime(2026, 7, 1);
        var friday = new DateTime(2026, 7, 3);
        var thursday = new DateTime(2026, 7, 2);
        var weekday = new DateTime(2026, 7, 10);
        var holidays = new HashSet<DateTime> { friday };

        var needy = MakeUser(11, exactNights: 1, exactHolidayNights: 1);
        // ExactNight=3 روی ۳ شب: ۲ تعطیل + ۱ عادی → مازاد تعطیل، بدون مازاد کل
        var donor = MakeUser(8, exactNights: 3, exactHolidayNights: 1);
        var filler = MakeUser(5, exactNights: null, exactHolidayNights: null);

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(10),
            HolidayDates = holidays,
            UserConstraints = [needy, donor, filler],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(11, 3, weekday, ShiftLabel.Night, false);
        solution.AddAssignment(8, 3, thursday, ShiftLabel.Night, false);
        solution.AddAssignment(8, 3, friday, ShiftLabel.Night, false);
        solution.AddAssignment(8, 3, new DateTime(2026, 7, 6), ShiftLabel.Night, false);

        // بقیه روزها پر تا صندلی خالی نباشد
        foreach (var day in Enumerable.Range(0, 11).Select(i => start.AddDays(i)))
        {
            if (solution.GetShiftAssignments(3, day).Any(a => !a.IsOnCall))
            {
                continue;
            }

            solution.AddAssignment(5, 3, day, ShiftLabel.Night, false);
        }

        Assert.Equal(3, solution.GetUserAllAssignments(8).Count(a => a.ShiftLabel == ShiftLabel.Night));
        Assert.Equal(1, solution.GetUserAllAssignments(11).Count(a => a.ShiftLabel == ShiftLabel.Night));

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var u11 = solution.GetUserAllAssignments(11).Where(a => a.ShiftLabel == ShiftLabel.Night).ToList();
        Assert.True(u11.Count >= 1);
        Assert.Contains(u11, a => HolidayWeekendNightRules.IsHolidayWeekendNight(a.Date, holidays));
        Assert.True(
            solution.GetUserAllAssignments(8).Count(a => a.ShiftLabel == ShiftLabel.Night) >= 3,
            "Donor total nights must stay at ExactNight floor after swap");
        Assert.True(
            solution.GetUserAllAssignments(8).Count(a =>
                a.ShiftLabel == ShiftLabel.Night &&
                HolidayWeekendNightRules.IsHolidayWeekendNight(a.Date, holidays)) >= 1,
            "Donor must keep holiday minimum");
    }

    [Fact]
    public void ExactNightQuotaGuard_DoesNotAddHolidayNightAboveExactTotal()
    {
        // ۵ شب محافظت‌شدهٔ غیرتعطیل + سهمیه تعطیل ۱ ⇒ نباید شب ششم اضافه شود (ظرفیت ماه را می‌دزدد)
        var start = new DateTime(2026, 7, 23);
        var end = new DateTime(2026, 8, 22);
        var holidays = new HashSet<DateTime>
        {
            new(2026, 7, 24), new(2026, 7, 31), new(2026, 8, 7), new(2026, 8, 14), new(2026, 8, 21)
        };

        var locked = MakeUser(6, exactNights: 5, exactHolidayNights: 1);
        locked.RequiredShiftSlots =
        [
            new ShiftSlotConstraint { Date = new DateTime(2026, 7, 25), ShiftLabel = ShiftLabel.Night },
            new ShiftSlotConstraint { Date = new DateTime(2026, 8, 1), ShiftLabel = ShiftLabel.Night },
            new ShiftSlotConstraint { Date = new DateTime(2026, 8, 8), ShiftLabel = ShiftLabel.Night },
            new ShiftSlotConstraint { Date = new DateTime(2026, 8, 15), ShiftLabel = ShiftLabel.Night },
            new ShiftSlotConstraint { Date = new DateTime(2026, 8, 22), ShiftLabel = ShiftLabel.Night }
        ];

        var needy = MakeUser(4, exactNights: 5, exactHolidayNights: 1);
        var filler = MakeUser(9, exactNights: 21, exactHolidayNights: null);

        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            HolidayDates = holidays,
            UserConstraints = [locked, needy, filler],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        foreach (var slot in locked.RequiredShiftSlots)
        {
            solution.AddAssignment(6, 3, slot.Date, ShiftLabel.Night, false);
        }

        // needy چهار شب؛ یک شب کم
        foreach (var d in new[]
                 {
                     new DateTime(2026, 7, 26),
                     new DateTime(2026, 8, 2),
                     new DateTime(2026, 8, 9),
                     new DateTime(2026, 8, 16)
                 })
        {
            solution.AddAssignment(4, 3, d, ShiftLabel.Night, false);
        }

        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            if (solution.GetShiftAssignments(3, day).Any())
            {
                continue;
            }

            solution.AddAssignment(9, 3, day, ShiftLabel.Night, false);
        }

        // یک مازاد روی filler تا needy بتواند ادعا کند
        // filler روی کف ۲۱ است — یک شب را به کاربر بدون سهمیه نده؛ به‌جای آن quota filler را ۲۰ کن
        filler.ExactNightShiftCount = 20;

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var nights6 = solution.GetUserAllAssignments(6).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        var nights4 = solution.GetUserAllAssignments(4).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);

        Assert.Equal(5, nights6); // نباید ۶ شود
        Assert.True(nights4 >= 5, $"User 4 expected ≥5 after reclaim, got {nights4}");
    }

    [Fact]
    public void NightQuotaRequestLinker_RejectsNonHolidayOnsThatBlockHolidayQuota()
    {
        var error = NightQuotaRequestLinker.ValidateNonHolidayOnLeavesRoomForHoliday(
            approvedNonHolidayNightCountInMonth: 4,
            exactNightQuota: 5,
            exactHolidayNightQuota: 1,
            persianYear: 1405,
            persianMonth: 5,
            pendingAdditionalNonHoliday: 1);

        Assert.NotNull(error);
        Assert.Contains("حداکثر 4", error);
    }

    [Fact]
    public void NightQuotaRequestLinker_QuotaUpsert_RejectsWhenBelowApprovedOrBlocksHolidayRoom()
    {
        var below = NightQuotaRequestLinker.ValidateQuotaAgainstApprovedNightRequests(
            newNightQuota: 3,
            approvedNightCount: 5,
            newHolidayQuota: 1,
            approvedHolidayNightCount: 0,
            approvedNonHolidayNightCount: 5,
            persianYear: 1405,
            persianMonth: 5);
        Assert.NotNull(below);
        Assert.Contains("کمتر از تعداد درخواست", below);

        var room = NightQuotaRequestLinker.ValidateQuotaAgainstApprovedNightRequests(
            newNightQuota: 5,
            approvedNightCount: 5,
            newHolidayQuota: 1,
            approvedHolidayNightCount: 0,
            approvedNonHolidayNightCount: 5,
            persianYear: 1405,
            persianMonth: 5);
        Assert.NotNull(room);
        Assert.Contains("سازگار نیست", room);

        var clear = NightQuotaRequestLinker.ValidateQuotaAgainstApprovedNightRequests(
            newNightQuota: null,
            approvedNightCount: 2,
            newHolidayQuota: null,
            approvedHolidayNightCount: 0,
            approvedNonHolidayNightCount: 2,
            persianYear: 1405,
            persianMonth: 5);
        Assert.NotNull(clear);
        Assert.Contains("حذف", clear);

        var ok = NightQuotaRequestLinker.ValidateQuotaAgainstApprovedNightRequests(
            newNightQuota: 5,
            approvedNightCount: 4,
            newHolidayQuota: 1,
            approvedHolidayNightCount: 1,
            approvedNonHolidayNightCount: 3,
            persianYear: 1405,
            persianMonth: 5);
        Assert.Null(ok);
    }

    [Fact]
    public void ExactNightQuotaGuard_ClearsNextMorningToClaimSurplusNight()
    {
        // سناریوی واقعی: گیرنده ۴/۵، اهداکننده ۱+۱ مازاد؛ صبح فردای شب مازاد مانع ادعاست.
        // filler روی کف سهمیه است تا فقط اهدا از user 11 ممکن باشد.
        var start = new DateTime(2026, 7, 23);
        var end = new DateTime(2026, 8, 22);
        var needy = MakeUser(6, exactNights: 5, exactHolidayNights: null);
        var donor = MakeUser(11, exactNights: 1, exactHolidayNights: null);
        var filler = MakeUser(4, exactNights: 25, exactHolidayNights: null);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = end,
            HolidayDates = [new(2026, 7, 24), new(2026, 7, 31), new(2026, 8, 7), new(2026, 8, 14), new(2026, 8, 21)],
            UserConstraints = [needy, donor, filler],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        foreach (var d in new[]
                 {
                     new DateTime(2026, 7, 26),
                     new DateTime(2026, 8, 2),
                     new DateTime(2026, 8, 15),
                     new DateTime(2026, 8, 20)
                 })
        {
            solution.AddAssignment(6, 3, d, ShiftLabel.Night, false);
        }

        solution.AddAssignment(11, 3, new DateTime(2026, 8, 8), ShiftLabel.Night, false);
        solution.AddAssignment(11, 3, new DateTime(2026, 7, 28), ShiftLabel.Night, false);
        solution.AddAssignment(6, 1, new DateTime(2026, 8, 9), ShiftLabel.Morning, false);

        foreach (var day in Enumerable.Range(0, 31).Select(i => start.AddDays(i)))
        {
            if (solution.GetShiftAssignments(3, day).Any())
            {
                continue;
            }

            solution.AddAssignment(4, 3, day, ShiftLabel.Night, false);
        }

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var nights6 = solution.GetUserAllAssignments(6).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        var nights11 = solution.GetUserAllAssignments(11).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        Assert.True(nights6 >= 5, $"User 6 expected ≥5 nights, got {nights6}");
        Assert.True(nights11 >= 1, $"Donor must keep minimum, got {nights11}");
        var claimedAug8 = solution.GetUserAllAssignments(6).Any(a =>
            a.ShiftLabel == ShiftLabel.Night && a.Date.Date == new DateTime(2026, 8, 8));
        if (claimedAug8)
        {
            Assert.False(
                solution.GetUserAllAssignments(6).Any(a =>
                    a.ShiftLabel == ShiftLabel.Morning && a.Date.Date == new DateTime(2026, 8, 9)),
                "Next-day morning conflicting with claimed night should be cleared");
        }
    }

    [Fact]
    public void ExactNightQuotaGuard_TwoHopChainWhenSurplusNotOnReceiverDate()
    {
        // R کم دارد، O روی تاریخ مناسب R ولی روی کف سهمیه است، S روی تاریخ دیگر مازاد دارد.
        var start = new DateTime(2026, 8, 1);
        var r = MakeUser(1, exactNights: 2, exactHolidayNights: 0);
        var bridge = MakeUser(2, exactNights: 1, exactHolidayNights: 0);
        var surplus = MakeUser(3, exactNights: 1, exactHolidayNights: 0);
        var constraints = new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(10),
            UserConstraints = [r, bridge, surplus],
            ShiftRequirements =
            [
                Shift(1, ShiftLabel.Morning),
                Shift(2, ShiftLabel.Evening),
                Shift(3, ShiftLabel.Night)
            ]
        };

        var solution = new ShiftSolution();
        solution.AddAssignment(1, 3, start, ShiftLabel.Night, false);          // R: 1/2
        solution.AddAssignment(2, 3, start.AddDays(3), ShiftLabel.Night, false); // bridge روی کف
        solution.AddAssignment(3, 3, start.AddDays(6), ShiftLabel.Night, false); // S حداقل
        solution.AddAssignment(3, 3, start.AddDays(9), ShiftLabel.Night, false); // S مازاد

        // همهٔ روزهای خالی را با کاربر بدون‌سهمیه پر کن تا فقط زنجیره ممکن باشد
        var filler = MakeUser(9, exactNights: null, exactHolidayNights: null);
        constraints.UserConstraints.Add(filler);
        foreach (var day in Enumerable.Range(0, 11).Select(i => start.AddDays(i)))
        {
            if (solution.GetShiftAssignments(3, day).Any())
            {
                continue;
            }

            solution.AddAssignment(9, 3, day, ShiftLabel.Night, false);
        }

        ExactNightQuotaGuard.Enforce(solution, constraints);

        var rNights = solution.GetUserAllAssignments(1).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        var bridgeNights = solution.GetUserAllAssignments(2).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        var surplusNights = solution.GetUserAllAssignments(3).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);

        Assert.True(rNights >= 2, $"Receiver expected ≥2, got {rNights}");
        Assert.True(bridgeNights >= 1, $"Bridge expected ≥1, got {bridgeNights}");
        Assert.True(surplusNights >= 1, $"Surplus donor expected ≥1, got {surplusNights}");
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
