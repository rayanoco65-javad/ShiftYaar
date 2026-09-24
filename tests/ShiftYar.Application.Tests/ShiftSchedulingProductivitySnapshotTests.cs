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
            },
            CapBaseHoursToStandardMonth = true
        };
    }

    [Fact]
    public void CalculateProductivitySnapshot_FereshtehSaki_Calculates145Hours()
    {
        // فرشته ساکی: ۲۲ سال سابقه (۵ ساعت کسر سابقه) + صعوبت ۱۰۰٪ (۲ ساعت) + ثابت صبح (۰ ساعت کسر نوبت‌کاری) = ۷ ساعت تخفیف هفتگی
        // ساعت موظفی: ۱۴۵ ساعت (مبنای ۱۷۶ منهای ۳۱ = ۱۴۵)
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 12,
            FullName = "فرشته ساکی",
            IncludedProductivityPlan = true,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            HardshipPercent = 100m,
            DateOfEmployment = new DateTime(2004, 8, 21) // 22 years
        };
        var userConstraint = new UserConstraint
        {
            UserId = 12,
            UserName = "فرشته ساکی",
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            ExperienceYears = 22,
            HardshipPercent = 100m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Breakdown?.IsCappedToStandardMonth);
        Assert.Equal(24, snapshot.Breakdown?.WorkingDays);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(7.0m, snapshot.Breakdown?.TotalWeeklyReduction);
        Assert.Equal(145m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(145, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(145m, userConstraint.ProductivityRequiredHours);
    }

    [Fact]
    public void CalculateProductivitySnapshot_FatemehRezaei_Calculates149Hours()
    {
        // فاطمه رضایی: ۱۱ سال سابقه (۳ ساعت کسر سابقه) + صعوبت ۱۰۰٪ (۲ ساعت) + ۳ شیفت گردشی (۱ ساعت کسر نوبت‌کاری) = ۶ ساعت تخفیف هفتگی
        // ساعت موظفی: ۱۴۹ ساعت (مبنای ۱۷۶ منهای ۲۶.۵۷ = ۱۴۹.۴۳ -> گرد شده: ۱۴۹)
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 17,
            FullName = "فاطمه رضایی",
            IncludedProductivityPlan = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            HardshipPercent = 100m,
            DateOfEmployment = new DateTime(2015, 5, 21) // 11 years
        };
        var userConstraint = new UserConstraint
        {
            UserId = 17,
            UserName = "فاطمه رضایی",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            ExperienceYears = 11,
            HardshipPercent = 100m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Breakdown?.IsCappedToStandardMonth);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(6.0m, snapshot.Breakdown?.TotalWeeklyReduction);
        Assert.Equal(149m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(149, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(149m, userConstraint.ProductivityRequiredHours);
    }

    [Fact]
    public void CalculateProductivitySnapshot_BaharBahari_Calculates141Hours()
    {
        // بهار بهاری: ۱۸ سال سابقه (۵ ساعت کسر سابقه) + صعوبت ۱۰۰٪ (۲ ساعت) + ۳ شیفت گردشی (۱ ساعت کسر نوبت‌کاری) = سقف ۸ ساعت تخفیف هفتگی
        // ساعت موظفی: ۱۴۱ ساعت (مبنای ۱۷۶ منهای ۳۵.۴۳ = ۱۴۰.۵۷ -> گرد شده: ۱۴۱)
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 14,
            FullName = "بهار بهاری",
            IncludedProductivityPlan = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            HardshipPercent = 100m,
            DateOfEmployment = new DateTime(2008, 6, 21) // 18 years
        };
        var userConstraint = new UserConstraint
        {
            UserId = 14,
            UserName = "بهار بهاری",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            ExperienceYears = 18,
            HardshipPercent = 100m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Breakdown?.IsCappedToStandardMonth);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(8.0m, snapshot.Breakdown?.TotalWeeklyReduction);
        Assert.Equal(141m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(141, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(141m, userConstraint.ProductivityRequiredHours);
    }

    [Fact]
    public void CalculateProductivitySnapshot_JuniorNurseUnder5Years_Calculates158Hours()
    {
        // پرسنل زیر ۵ سال سابقه خدمت (۰ تا ۴ سال): ۱ ساعت کسر سنوات + ۲ ساعت صعوبت + ۱ ساعت نوبت‌کاری = ۴ ساعت تخفیف هفتگی
        // ساعت موظفی: ۱۵۸ ساعت (مبنای ۱۷۶ منهای ۱۷.۷۱ = ۱۵۸.۲۹ -> گرد شده: ۱۵۸)
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 23,
            FullName = "مریم کرمی",
            IncludedProductivityPlan = true,
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            HardshipPercent = 100m,
            DateOfEmployment = new DateTime(2022, 7, 22) // 4 years
        };
        var userConstraint = new UserConstraint
        {
            UserId = 23,
            UserName = "مریم کرمی",
            ShiftType = ShiftTypes.RotatingShift,
            ShiftSubType = ShiftSubTypes.ThreeShifts,
            ExperienceYears = 4,
            HardshipPercent = 100m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Breakdown?.IsIncludedInProductivityPlan);
        Assert.Equal(4.0m, snapshot.Breakdown?.TotalWeeklyReduction);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(158.00m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(158, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(158.00m, userConstraint.ProductivityRequiredHours);
    }

    [Fact]
    public void CalculateProductivitySnapshot_NonClinicalPersonnel_Calculates176Hours()
    {
        // پرسنل غیرمشمول قانون ارتقای بهره‌وری (عادی): تخفیف هفتگی ۰ -> موظفی پایه ۱۷۶ ساعت
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 99,
            FullName = "کارمند اداری",
            IncludedProductivityPlan = false,
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            HardshipPercent = 0m
        };
        var userConstraint = new UserConstraint
        {
            UserId = 99,
            UserName = "کارمند اداری",
            ShiftType = ShiftTypes.FixedShift,
            ShiftSubType = ShiftSubTypes.FixedMorning,
            ExperienceYears = 10,
            HardshipPercent = 0m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.False(snapshot.Breakdown?.IsIncludedInProductivityPlan);
        Assert.Equal(0.0m, snapshot.Breakdown?.TotalWeeklyReduction);
        Assert.Equal(176.00m, snapshot.BaseMonthlyHours);
        Assert.Equal(176.00m, snapshot.FinalMonthlyRequiredHours);
        Assert.Equal(176, snapshot.FinalMonthlyRequiredHoursRounded);

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, userConstraint, snapshot);
        Assert.Equal(176.00m, userConstraint.ProductivityRequiredHours);
    }

    [Theory]
    [InlineData("مهدی رستمی", 1, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("محمد یوسفی", 1, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("مهدی دریکوند", 1, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("سیده زهرا باقری", 1, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("فائزه سبزواری", 2, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("سیده فاطمه کاظمی", 2, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("فاطمه دبستانیان", 2, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("شکیبا موسیوند", 3, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("مریم کرمی", 4, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("مریم امیدی منش", 4, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 158)]
    [InlineData("فاطمه رازانی", 5, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 154)]
    [InlineData("حدیث کاظمی", 10, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 149)]
    [InlineData("عاطفه رحیمی منفرد", 10, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 149)]
    [InlineData("فاطمه رضایی", 11, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 149)]
    [InlineData("خدیجه متقی", 11, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 149)]
    [InlineData("فاطمه سلیمی", 13, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 145)]
    [InlineData("فرشته ساکی", 22, ShiftTypes.FixedShift, ShiftSubTypes.FixedMorning, 145)]
    [InlineData("صبا حاتمی فیضی", 24, ShiftTypes.FixedShift, ShiftSubTypes.FixedMorning, 145)]
    [InlineData("زهرا درخشانی الوار", 25, ShiftTypes.RotatingShift, ShiftSubTypes.TwoShifts, 141)]
    [InlineData("بهاره بهاری پور", 18, ShiftTypes.RotatingShift, ShiftSubTypes.ThreeShifts, 141)]
    public void CalculateProductivitySnapshot_AllHospitalStaff_MatchExactHospitalDutyHours(
        string fullName,
        int experienceYears,
        ShiftTypes shiftType,
        ShiftSubTypes subType,
        int expectedHospitalHours)
    {
        var constraints = CreateShahrivar1405Constraints();
        var user = new User
        {
            Id = 999,
            FullName = fullName,
            IncludedProductivityPlan = true,
            ShiftType = shiftType,
            ShiftSubType = subType,
            HardshipPercent = 100m
        };
        var userConstraint = new UserConstraint
        {
            UserId = 999,
            UserName = fullName,
            ShiftType = shiftType,
            ShiftSubType = subType,
            ExperienceYears = experienceYears,
            HardshipPercent = 100m
        };

        var snapshot = _service.CalculateProductivitySnapshot(user, userConstraint, constraints, deptSetting: null, nightShiftDurationHours: 12.0);

        Assert.NotNull(snapshot);
        Assert.Equal(expectedHospitalHours, snapshot.FinalMonthlyRequiredHoursRounded);
    }
}
