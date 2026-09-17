using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ProductivityModel.Services;
using ShiftYar.Application.Features.ShiftModel.Services;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.UserModel;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ShiftSchedulingProductivitySnapshotTests
{
    private readonly ShiftSchedulingService _service;
    private readonly WorkingHoursCalculator _calculator;

    public ShiftSchedulingProductivitySnapshotTests()
    {
        _calculator = new WorkingHoursCalculator();
        _service = new ShiftSchedulingService(
            userRepository: null!,
            monthlyNightQuotaRepository: null!,
            monthlyDayShiftQuotaRepository: null!,
            monthlyComboShiftQuotaRepository: null!,
            shiftRepository: null!,
            departmentRepository: null!,
            deptSettingsRepository: null!,
            specialtyRepository: null!,
            shiftRequiredSpecialtyRepository: null!,
            shiftAssignmentRepository: null!,
            shiftDateRepository: null!,
            shiftRequestRepository: null!,
            algorithmSettingsService: null!,
            workingHoursCalculator: _calculator,
            mapper: null!,
            schedulingJobStore: null!,
            httpContextAccessor: null!,
            logger: NullLogger<ShiftSchedulingService>.Instance);
    }

    private static ShiftConstraints CreateShahrivar1405Constraints()
    {
        // شهریور ۱۴۰۵: ۳۱ روز (۱۴۰۵/۰۶/۰۱ تا ۱۴۰۵/۰۶/۳۱)
        // تقویم میلادی: 2026-08-23 تا 2026-09-22
        // ۴ جمعه: 2026-08-28, 2026-09-04, 2026-09-11, 2026-09-18
        // ۱ تعطیل رسمی: 2026-08-30 (۸ شهریور - اربعین)
        return new ShiftConstraints
        {
            DepartmentId = 1,
            StartDate = new DateTime(2026, 8, 23),
            EndDate = new DateTime(2026, 9, 22),
            HolidayDates = new HashSet<DateTime>
            {
                new DateTime(2026, 8, 30) // ۸ شهریور
            }
        };
    }

    [Fact]
    public void CalculateProductivitySnapshot_FereshtehSaki_Calculates163Hours()
    {
        // فرشته ساکی: ۸ سال سابقه (۱ ساعت کسر سابقه) + بخش عادی (۱ ساعت کسر سختی) + ۳ شیفت گردشی (۱ ساعت کسر نوبت‌کاری) = ۳ ساعت تخفیف هفتگی
        // ساعت موظفی بیمارستان: ۱۶۳ ساعت (مبنای ۱۷۶ منهای ۱۳.۲۹ = ۱۶۲.۷۱)
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 101,
            FullName = "فرشته ساکی",
            IncludedProductivityPlan = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            HardshipPercent = 0m,
            DateOfEmployment = new DateTime(2018, 8, 23) // 8 years before 2026
        };
        var userConstraint = new UserConstraint
        {
            UserId = 101,
            UserName = "فرشته ساکی",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            ExperienceYears = 8,
            HardshipPercent = 0m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Breakdown?.IsCappedToStandardMonth);
        Assert.Equal(24, snapshot.Breakdown?.WorkingDays);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(3.0m, snapshot.Breakdown?.TotalWeeklyReduction);
        Assert.Equal(162.71m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(163, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(162.71m, userConstraint.ProductivityRequiredHours);
    }

    [Fact]
    public void CalculateProductivitySnapshot_FatemehRezaei_Calculates167Hours()
    {
        // فاطمه رضایی: ۲ سال سابقه (۰ ساعت کسر سابقه) + بخش عادی (۱ ساعت کسر سختی) + ۳ شیفت گردشی (۱ ساعت کسر نوبت‌کاری) = ۲ ساعت تخفیف هفتگی
        // ساعت موظفی بیمارستان: ۱۶۷ ساعت (مبنای ۱۷۶ منهای ۸.۸۶ = ۱۶۷.۱۴)
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 102,
            FullName = "فاطمه رضایی",
            IncludedProductivityPlan = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            HardshipPercent = 0m,
            DateOfEmployment = new DateTime(2024, 8, 23) // 2 years before 2026
        };
        var userConstraint = new UserConstraint
        {
            UserId = 102,
            UserName = "فاطمه رضایی",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            ExperienceYears = 2,
            HardshipPercent = 0m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Breakdown?.IsCappedToStandardMonth);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(2.0m, snapshot.Breakdown?.TotalWeeklyReduction);
        Assert.Equal(167.14m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(167, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(167.14m, userConstraint.ProductivityRequiredHours);
    }

    [Fact]
    public void CalculateProductivitySnapshot_BaharBahari_Calculates158Hours()
    {
        // بهار بهاری: ۱۸ سال سابقه (۲ ساعت کسر سابقه) + بخش عادی (۱ ساعت کسر سختی) + ۳ شیفت گردشی (۱ ساعت کسر نوبت‌کاری) = ۴ ساعت تخفیف هفتگی
        // ساعت موظفی بیمارستان: ۱۵۸ ساعت (مبنای ۱۷۶ منهای ۱۷.۷۱ = ۱۵۸.۲۹)
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 103,
            FullName = "بهار بهاری",
            IncludedProductivityPlan = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            HardshipPercent = 0m,
            DateOfEmployment = new DateTime(2008, 8, 23) // 18 years
        };
        var userConstraint = new UserConstraint
        {
            UserId = 103,
            UserName = "بهار بهاری",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            ExperienceYears = 18,
            HardshipPercent = 0m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Breakdown?.IsCappedToStandardMonth);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(4.0m, snapshot.Breakdown?.TotalWeeklyReduction);
        Assert.Equal(158.29m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(158, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(158.29m, userConstraint.ProductivityRequiredHours);
    }

    [Fact]
    public void CalculateProductivitySnapshot_OrdinaryStaff_Calculates176Hours()
    {
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 104,
            FullName = "مریم کرمی",
            IncludedProductivityPlan = false
        };
        var userConstraint = new UserConstraint
        {
            UserId = 104,
            UserName = "مریم کرمی",
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.False(snapshot.Breakdown?.IsIncludedInProductivityPlan);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(176.00m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(176, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(176.00m, userConstraint.ProductivityRequiredHours);
    }
}
