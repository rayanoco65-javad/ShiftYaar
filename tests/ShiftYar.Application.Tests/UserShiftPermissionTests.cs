using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class UserShiftPermissionTests
{
    private static UserConstraint MinaLikeUser() => new()
    {
        UserId = 1,
        AllowedShiftPermissions =
            UserShiftPermission.Morning
            | UserShiftPermission.Evening
            | UserShiftPermission.MorningNightSameDay
    };

    [Fact]
    public void Mina_MorningAndEveningStandalone_Allowed()
    {
        var user = MinaLikeUser();
        Assert.True(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [], ShiftLabel.Morning, 2));
        Assert.True(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [], ShiftLabel.Evening, 2));
    }

    [Fact]
    public void Mina_NightStandalone_NotAllowed()
    {
        var user = MinaLikeUser();
        Assert.False(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [], ShiftLabel.Night, 2));
    }

    [Fact]
    public void Mina_MorningEveningSameDay_NotAllowed()
    {
        var user = MinaLikeUser();
        Assert.False(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [ShiftLabel.Morning], ShiftLabel.Evening, 2));
    }

    [Fact]
    public void Mina_MorningNightSameDay_Allowed()
    {
        var user = MinaLikeUser();
        Assert.True(ShiftEligibilityResolver.CanAssignShift(
            user.AllowedShiftPermissions, [ShiftLabel.Morning], ShiftLabel.Night, 2));
    }

    [Fact]
    public void MayEverTakeLabel_NightTrueWhenMorningNightComboWithoutStandaloneNight()
    {
        var user = MinaLikeUser();
        Assert.True(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night));
    }

    [Fact]
    public void NormalizePermissions_PreservesIndependentFlags_DoesNotAutoAddSingles()
    {
        var normalized = ShiftEligibilityResolver.NormalizePermissions(
            UserShiftPermission.MorningEveningSameDay);
        Assert.True(normalized.HasFlag(UserShiftPermission.MorningEveningSameDay));
        Assert.False(normalized.HasFlag(UserShiftPermission.Morning));
        Assert.False(normalized.HasFlag(UserShiftPermission.Evening));

        var nightCombo = ShiftEligibilityResolver.NormalizePermissions(
            UserShiftPermission.MorningNightSameDay);
        Assert.True(nightCombo.HasFlag(UserShiftPermission.MorningNightSameDay));
        Assert.False(nightCombo.HasFlag(UserShiftPermission.Morning));
        Assert.False(nightCombo.HasFlag(UserShiftPermission.Night));
    }

    [Fact]
    public void FixedMorning_InherentPermission_OnlyMorning()
    {
        var user = new UserConstraint
        {
            UserId = 10,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night | UserShiftPermission.MorningEveningSameDay
        };

        Assert.True(ShiftEligibilityResolver.HasInherentPermission(user, ShiftLabel.Morning));
        Assert.False(ShiftEligibilityResolver.HasInherentPermission(user, ShiftLabel.Evening));
        Assert.False(ShiftEligibilityResolver.HasInherentPermission(user, ShiftLabel.Night));
    }

    [Fact]
    public void FixedMorning_MayTakeLabelOnDate_OnlyWithApprovedRequest()
    {
        var testDate = new DateTime(2026, 8, 25);
        var otherDate = new DateTime(2026, 8, 26);
        var user = new UserConstraint
        {
            UserId = 10,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
        };
        user.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = testDate,
            ShiftLabel = ShiftLabel.Evening
        });

        Assert.True(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Evening, testDate));
        Assert.False(ShiftEligibilityResolver.MayTakeLabelOnDate(user, ShiftLabel.Evening, otherDate));
    }

    [Fact]
    public void FixedMorning_IsAssignmentAllowed_RejectsUnrequestedNonFixedShifts()
    {
        var testDate = new DateTime(2026, 8, 25);
        var user = new UserConstraint
        {
            UserId = 10,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
        };

        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Morning, 2, date: testDate));
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Evening, 2, date: testDate));
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Morning], ShiftLabel.Evening, 2, date: testDate));

        // با داشتن درخواست تأییدشده برای عصر در آن تاریخ
        user.RequiredShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = testDate,
            ShiftLabel = ShiftLabel.Evening
        });
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Morning], ShiftLabel.Evening, 2, date: testDate));
    }

    [Fact]
    public void MapLegacy_ThreeShifts_IncludesAllPermissions()
    {
        var mapped = ShiftEligibilityResolver.MapLegacyToPermissions(
            ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, null);
        Assert.Equal(ShiftEligibilityResolver.AllPermissions, mapped);
    }

    [Theory]
    [InlineData(ShiftTypes.FixedShift, ShiftSubTypes.FixedMorning, null, UserShiftPermission.Morning)]
    [InlineData(ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, TwoShiftRotationPattern.MorningEvening,
        UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay)]
    public void MapLegacy_MatchesExpected(
        ShiftTypes type,
        ShiftSubTypes subType,
        TwoShiftRotationPattern? pattern,
        UserShiftPermission expected)
    {
        Assert.Equal(expected, ShiftEligibilityResolver.MapLegacyToPermissions(type, subType, pattern));
    }

    [Fact]
    public void RotatingUser_WithMorningEveningAndCombo_PermitsMorningEveningAndCombo_RejectsNight()
    {
        var user = new UserConstraint
        {
            UserId = 20,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.TwoShifts,
            TwoShiftRotationPattern = TwoShiftRotationPattern.MorningEvening,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.MorningEveningSameDay
        };

        Assert.True(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Morning));
        Assert.True(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Evening));
        Assert.False(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night));

        // Can assign single Morning
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Morning, 2));
        // Can assign single Evening
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Evening, 2));
        // Cannot assign single Night
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Night, 2));
        // Can assign Morning + Evening same day
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Morning], ShiftLabel.Evening, 2));
        // Cannot assign Morning + Night same day
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Morning], ShiftLabel.Night, 2));
    }

    [Fact]
    public void RotatingUser_WithMorningAndCombo_WithoutStandaloneEvening_PermitsComboOnly_RejectsStandaloneEvening()
    {
        var user = new UserConstraint
        {
            UserId = 21,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.TwoShifts,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.MorningEveningSameDay
        };

        Assert.True(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Morning));
        Assert.True(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Evening)); // Allowed via combo
        Assert.False(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night));

        // Single Morning: allowed
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Morning, 2));
        // Single Evening alone: NOT allowed
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Evening, 2));
        // Evening when Morning already present: allowed as combo!
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Morning], ShiftLabel.Evening, 2));
    }

    [Fact]
    public void RotatingUser_WithMorningAndComboNight_WithoutStandaloneNight_PermitsComboOnly_RejectsStandaloneNight()
    {
        var user = new UserConstraint
        {
            UserId = 22,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.TwoShifts,
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.MorningNightSameDay
        };

        Assert.True(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Morning));
        Assert.False(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Evening));
        Assert.True(ShiftEligibilityResolver.MayEverTakeLabel(user, ShiftLabel.Night)); // Allowed via combo

        // Single Morning: allowed
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Morning, 2));
        // Single Night alone: NOT allowed
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Night, 2));
        // Night when Morning already present: allowed as combo!
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Morning], ShiftLabel.Night, 2));
    }

    [Fact]
    public void RotatingUser_WithOnlySinglePermissions_RejectsAllCombosEvenWithMaxShiftsPerDayTwo()
    {
        var user = new UserConstraint
        {
            UserId = 23,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            // فقط گزینه‌های صبح تکی، عصر تکی و شب تکی فعال هستند (هیچ گزینه ترکیبی فعال نیست)
            AllowedShiftPermissions = UserShiftPermission.Morning | UserShiftPermission.Evening | UserShiftPermission.Night
        };

        // تک‌شیفت‌ها کاملاً مجازند
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Morning, maxShiftsPerDay: 2));
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Evening, maxShiftsPerDay: 2));
        Assert.True(ShiftEligibilityResolver.IsAssignmentAllowed(user, [], ShiftLabel.Night, maxShiftsPerDay: 2));

        // حتی با وجود maxShiftsPerDay = 2، هیچ ترکیب شیفتی در یک روز مجاز نیست
        Assert.False(ShiftEligibilityResolver.SupportsMorningEveningCombo(user, maxShiftsPerDay: 2));
        Assert.False(ShiftEligibilityResolver.SupportsMorningNightCombo(user));

        // تلاش برای اضافه کردن شیفت دوم به روزی که شیفت دارد باید مردود شود
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Morning], ShiftLabel.Evening, maxShiftsPerDay: 2));
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Morning], ShiftLabel.Night, maxShiftsPerDay: 2));
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Evening], ShiftLabel.Morning, maxShiftsPerDay: 2));
        Assert.False(ShiftEligibilityResolver.IsAssignmentAllowed(user, [ShiftLabel.Night], ShiftLabel.Morning, maxShiftsPerDay: 2));
    }
}

