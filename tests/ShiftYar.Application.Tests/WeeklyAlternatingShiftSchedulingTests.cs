using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.Services;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Application.Features.UserModel.Services;
using System;
using System.Collections.Generic;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class WeeklyAlternatingShiftSchedulingTests
{
    [Fact]
    public void IsAssignmentAllowed_WeeklyAlternatingActive_EnforcesCorrectShiftPerWeek()
    {
        var rangeStart = new DateTime(2026, 3, 21); // شنبه (هفته ۱)
        var week1Date = new DateTime(2026, 3, 22);  // یکشنبه (هفته ۱)
        var week2Date = new DateTime(2026, 3, 29);  // یکشنبه (هفته ۲)

        var user = new UserConstraint
        {
            UserId = 10,
            UserName = "خدیجه متقی",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            IsWeeklyAlternatingActive = true,
            FirstWeekShiftLabel = ShiftLabel.Morning
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(
            user,
            UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night);

        // هفته ۱:
        // صبح مجاز است
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Morning, maxShiftsPerDay: 1, date: week1Date, rangeStartDate: rangeStart));
        // عصر غیرمجاز است
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Evening, maxShiftsPerDay: 1, date: week1Date, rangeStartDate: rangeStart));
        // شب کاملاً مجاز است (استثنای قانون)
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Night, maxShiftsPerDay: 1, date: week1Date, rangeStartDate: rangeStart));

        // هفته ۲:
        // صبح غیرمجاز است
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Morning, maxShiftsPerDay: 1, date: week2Date, rangeStartDate: rangeStart));
        // عصر مجاز است
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Evening, maxShiftsPerDay: 1, date: week2Date, rangeStartDate: rangeStart));
        // شب کاملاً مجاز است
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Night, maxShiftsPerDay: 1, date: week2Date, rangeStartDate: rangeStart));
    }

    [Fact]
    public void IsAssignmentAllowed_ApprovedRequestSlot_BypassesWeeklyAlternatingOnRequestedDate()
    {
        var rangeStart = new DateTime(2026, 3, 21);
        var week1Date = new DateTime(2026, 3, 22); // هفته ۱ (سهمیه عادی: صبح)

        var user = new UserConstraint
        {
            UserId = 10,
            IsWeeklyAlternatingActive = true,
            FirstWeekShiftLabel = ShiftLabel.Morning
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(
            user,
            UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night);

        // بدون درخواست: عصر در هفته ۱ مجاز نیست
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Evening, maxShiftsPerDay: 1, date: week1Date, rangeStartDate: rangeStart));

        // با درخواست تأییدشده برای همین تاریخ: عصر مجاز می‌شود
        user.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = week1Date,
            ShiftLabel = ShiftLabel.Evening
        });

        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Evening, maxShiftsPerDay: 1, date: week1Date, rangeStartDate: rangeStart));
    }

    [Fact]
    public void IsAssignmentAllowed_KhadijehMotaghi_Week2EveningQuota_AllowsApprovedMorningRequest()
    {
        // سناریوی کاربر: کاربر «خدیجه متقی» در هفته دوم طبق تناوب فقط شیفت عصر دارد.
        // با داشتن درخواست شیفت صبح تأییدشده در آن هفته، شیفت صبح در برنامه اعمال و مجاز شمرده می‌شود.
        var rangeStart = new DateTime(2026, 3, 21); // شنبه
        var week2Date = new DateTime(2026, 3, 29);  // یکشنبه (هفته ۲ - سهمیه عادی: عصر)

        var user = new UserConstraint
        {
            UserId = 10,
            UserName = "خدیجه متقی",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            IsWeeklyAlternatingActive = true,
            FirstWeekShiftLabel = ShiftLabel.Morning // هفته ۱ صبح -> هفته ۲ عصر
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(
            user,
            UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night);

        // ۱. قبل از درخواست تأییدشده: در هفته ۲ شیفت صبح غیرمجاز است
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Morning, maxShiftsPerDay: 1, date: week2Date, rangeStartDate: rangeStart));

        // ۲. ثبت درخواست شیفت صبح تأییدشده برای تاریخ هفته ۲
        user.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = week2Date,
            ShiftLabel = ShiftLabel.Morning,
            ShiftId = 1
        });

        // ۳. بررسی حل‌کننده صلاحیت: اکنون شیفت صبح برای این تاریخ کاملاً مجاز است
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(
            user, Array.Empty<ShiftLabel>(), ShiftLabel.Morning, maxShiftsPerDay: 1, date: week2Date, rangeStartDate: rangeStart));
        Assert.True(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Morning, week2Date, rangeStart));

        // ۴. بررسی گارد شیفت‌بندی: انتساب شیفت صبح به عنوان تخلف شمرده نمی‌شود و حذف نخواهد شد
        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = rangeStart,
            EndDate = rangeStart.AddDays(29),
            UserConstraints = new List<UserConstraint> { user }
        };
        var solution = new ShiftSolution();
        solution.AddAssignment(user.UserId, 1, week2Date, ShiftLabel.Morning);

        var violations = ShiftEligibilityGuard.GetViolations(solution, constraints);
        Assert.DoesNotContain(violations, v => v.Contains("تناوب هفتگی نقض شد"));

        ShiftEligibilityGuard.StripIneligibleAssignments(solution, constraints);
        Assert.Single(solution.GetUserAssignments(user.UserId, week2Date));
    }

    [Fact]
    public void MayTakeLabelOnDate_WeeklyAlternatingActive_ReflectsAllowedShift()
    {
        var rangeStart = new DateTime(2026, 3, 21);
        var week1Date = new DateTime(2026, 3, 22);

        var user = new UserConstraint
        {
            UserId = 10,
            IsWeeklyAlternatingActive = true,
            FirstWeekShiftLabel = ShiftLabel.Evening // شروع با عصر
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(
            user,
            UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night);

        // در هفته ۱، شیفت عصر مجاز است
        Assert.True(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Evening, week1Date, rangeStart));
        // در هفته ۱، شیفت صبح غیرمجاز است
        Assert.False(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Morning, week1Date, rangeStart));
        // شیفت شب آزاد است
        Assert.True(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Night, week1Date, rangeStart));
    }

    [Fact]
    public void ShiftEligibilityGuard_DetectsWeeklyAlternatingViolationAndStripsIt()
    {
        var rangeStart = new DateTime(2026, 3, 21); // شنبه
        var week1Date = new DateTime(2026, 3, 23);  // دوشنبه (هفته ۱)

        var user = new UserConstraint
        {
            UserId = 10,
            UserName = "خدیجه متقی",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            IsWeeklyAlternatingActive = true,
            FirstWeekShiftLabel = ShiftLabel.Morning // هفته ۱ صبح است
        };
        ShiftEligibilityResolver.ApplyPermissionsToUserConstraint(
            user,
            UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night);

        var constraints = new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = rangeStart,
            EndDate = rangeStart.AddDays(29),
            UserConstraints = new List<UserConstraint> { user }
        };

        var solution = new ShiftSolution();
        // انتساب نادرست شیفت عصر در هفته ۱
        solution.AddAssignment(10, 2, week1Date, ShiftLabel.Evening);

        // بررسی گزارش تخلف
        var violations = ShiftEligibilityGuard.GetViolations(solution, constraints);
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("تناوب هفتگی نقض شد") && v.Contains("خدیجه متقی") && v.Contains("هفته 1"));

        // پاکسازی انتساب غیرمجاز
        ShiftEligibilityGuard.StripIneligibleAssignments(solution, constraints);
        Assert.Empty(solution.GetUserAssignments(10, week1Date));
    }

    [Fact]
    public void ValidateWeeklyAlternatingPreference_ChecksRequiredConditions()
    {
        var userWithBoth = new ShiftYar.Domain.Entities.UserModel.User
        {
            Id = 1,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening
        };

        var userMorningOnly = new ShiftYar.Domain.Entities.UserModel.User
        {
            Id = 2,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Night
        };

        // ۱. فعال بدون تعیین شیفت اولیه
        var error1 = UserMonthlyDayShiftQuotaService.ValidateWeeklyAlternatingPreference(
            userWithBoth, true, null);
        Assert.NotNull(error1);
        Assert.Contains("شیفت آغازین", error1);

        // ۲. انتخاب شیفت نامعتبر (شب) به عنوان شیفت آغازین
        var error2 = UserMonthlyDayShiftQuotaService.ValidateWeeklyAlternatingPreference(
            userWithBoth, true, ShiftLabel.Night);
        Assert.NotNull(error2);
        Assert.Contains("تنها می‌تواند صبح یا عصر باشد", error2);

        // ۳. عدم دسترسی پرسنل به هر دو شیفت صبح و عصر
        var error3 = UserMonthlyDayShiftQuotaService.ValidateWeeklyAlternatingPreference(
            userMorningOnly, true, ShiftLabel.Morning);
        Assert.NotNull(error3);
        Assert.Contains("هر دو شیفت صبح و عصر", error3);

        // ۴. شرایط معتبر
        var validResult = UserMonthlyDayShiftQuotaService.ValidateWeeklyAlternatingPreference(
            userWithBoth, true, ShiftLabel.Morning);
        Assert.Null(validResult);
    }
}
