using System;
using System.Collections.Generic;
using System.Linq;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.DepartmentModel.Services;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Tests;

public class ShiftOvertimeSeniorityDistributionTests
{
    [Fact]
    public void OvertimeBalanceGuard_WhenOvertimeFriendly_AllocatesMoreOvertimeToSeniorUser()
    {
        // دپارتمان علاقه‌مند به اضافه کار (Type = 0): پرسنل با سابقه بیشتر اضافه کار بیشتری دریافت می‌کنند
        var start = new DateTime(2026, 9, 1);
        var senior = MakeUser(1, experienceYears: 20, requiredHours: 70, overtimeConsent: true);
        var junior = MakeUser(2, experienceYears: 1, requiredHours: 70, overtimeConsent: true);

        var constraints = BuildConstraints(start, [senior, junior], overtimeDistributionEnabled: true, overtimePreferenceType: 0);

        var solution = new ShiftSolution();
        // Senior روی روزهای زوج (0, 2, 4, ..., 24) = 13 شیفت = 91 ساعت (21 ساعت اضافه کار)
        // Junior روی روزهای فرد (1, 3, 5, ..., 25) = 13 شیفت = 91 ساعت (21 ساعت اضافه کار)
        for (var i = 0; i < 26; i++)
        {
            if (i % 2 == 0)
            {
                solution.AddAssignment(senior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
            }
            else
            {
                solution.AddAssignment(junior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
            }
        }

        OvertimeBalanceGuard.Enforce(solution, constraints);

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var seniorHours = OvertimeBalanceGuard.CalculateHours(solution, senior, lookup, constraints);
        var juniorHours = OvertimeBalanceGuard.CalculateHours(solution, junior, lookup, constraints);

        var seniorOt = seniorHours - (double)senior.ProductivityRequiredHours!.Value;
        var juniorOt = juniorHours - (double)junior.ProductivityRequiredHours!.Value;

        Assert.True(
            seniorOt > juniorOt,
            $"Expected senior overtime > junior overtime in OvertimeFriendly department; got seniorOt={seniorOt:F1}, juniorOt={juniorOt:F1}");
    }

    [Fact]
    public void OvertimeBalanceGuard_WhenOvertimeAvoiding_AllocatesMoreOvertimeToJuniorUser()
    {
        // دپارتمان گریزان از اضافه کار (Type = 1): پرسنل با سابقه کمتر اضافه کار بیشتری دریافت می‌کنند (محافظت از با‌سابقه‌ها)
        var start = new DateTime(2026, 9, 1);
        var senior = MakeUser(1, experienceYears: 20, requiredHours: 70, overtimeConsent: true);
        var junior = MakeUser(2, experienceYears: 1, requiredHours: 70, overtimeConsent: true);

        var constraints = BuildConstraints(start, [senior, junior], overtimeDistributionEnabled: true, overtimePreferenceType: 1);

        var solution = new ShiftSolution();
        // Senior روی روزهای زوج، Junior روی روزهای فرد
        for (var i = 0; i < 26; i++)
        {
            if (i % 2 == 0)
            {
                solution.AddAssignment(senior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
            }
            else
            {
                solution.AddAssignment(junior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
            }
        }

        OvertimeBalanceGuard.Enforce(solution, constraints);

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var seniorHours = OvertimeBalanceGuard.CalculateHours(solution, senior, lookup, constraints);
        var juniorHours = OvertimeBalanceGuard.CalculateHours(solution, junior, lookup, constraints);

        var seniorOt = seniorHours - (double)senior.ProductivityRequiredHours!.Value;
        var juniorOt = juniorHours - (double)junior.ProductivityRequiredHours!.Value;

        Assert.True(
            juniorOt > seniorOt,
            $"Expected junior overtime > senior overtime in OvertimeAvoiding department; got seniorOt={seniorOt:F1}, juniorOt={juniorOt:F1}");
    }

    [Fact]
    public void OvertimeBalanceGuard_WhenOvertimeAvoidingWithProductivityRequiredHoursDiff_ProtectsSeniorUser()
    {
        // سناریوی واقعی دپارتمان ۲:
        // پرسنل با سابقه ۱۳ سال با موظفی کمتر (۱۶۲.۷ ساعت) در برابر پرسنل ۳ سال با موظفی ۱۷۶ ساعت
        // در دپارتمان گریزان از اضافه کار، پرسنل کم‌سابقه اضافه کار بیشتری دریافت می‌کنند
        var start = new DateTime(2026, 9, 1);
        var senior = MakeUser(1, experienceYears: 13, requiredHours: 162.7m, overtimeConsent: true);
        var junior = MakeUser(2, experienceYears: 3, requiredHours: 176.0m, overtimeConsent: true);

        var constraints = BuildConstraints(start, [senior, junior], overtimeDistributionEnabled: true, overtimePreferenceType: 1);

        var solution = new ShiftSolution();
        // فرض اولیه: هر دو پرسنل به تعداد مساوی ۲۷ شیفت (۱۸۹ ساعت) شیفت دارند
        // ساعت اولیه سنیور: ۱۸۹ (اضافه کار: ۲۶.۳ ساعت)
        // ساعت اولیه جونیور: ۱۸۹ (اضافه کار: ۱۳ ساعت)
        for (var i = 0; i < 27; i++)
        {
            solution.AddAssignment(senior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
            solution.AddAssignment(junior.UserId, 1, start.AddDays(i + 28), ShiftLabel.Morning, false);
        }

        OvertimeBalanceGuard.Enforce(solution, constraints);

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var seniorHours = OvertimeBalanceGuard.CalculateHours(solution, senior, lookup, constraints);
        var juniorHours = OvertimeBalanceGuard.CalculateHours(solution, junior, lookup, constraints);

        var seniorOt = seniorHours - (double)senior.ProductivityRequiredHours!.Value;
        var juniorOt = juniorHours - (double)junior.ProductivityRequiredHours!.Value;

        Assert.True(
            juniorOt >= seniorOt,
            $"Expected junior overtime >= senior overtime in OvertimeAvoiding department; got seniorOt={seniorOt:F1}, juniorOt={juniorOt:F1}");
    }

    [Fact]
    public void OvertimeBalanceGuard_WhenNeutral_BalancesOvertimeEqually()
    {
        // دپارتمان خنثی (Type = 2): اضافه کار مساوی تقسیم می‌شود
        var start = new DateTime(2026, 9, 1);
        var senior = MakeUser(1, experienceYears: 20, requiredHours: 70, overtimeConsent: true);
        var junior = MakeUser(2, experienceYears: 1, requiredHours: 70, overtimeConsent: true);

        var constraints = BuildConstraints(start, [senior, junior], overtimeDistributionEnabled: true, overtimePreferenceType: 2);

        var solution = new ShiftSolution();
        // Senior دارای 17 شیفت (119 ساعت = 49 ساعت اضافه کار) روی روزهای زوج
        // Junior دارای 9 شیفت (63 ساعت = کسری 7 ساعت) روی روزهای فرد
        for (var i = 0; i < 34; i += 2)
        {
            solution.AddAssignment(senior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }
        for (var i = 1; i < 19; i += 2)
        {
            solution.AddAssignment(junior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        OvertimeBalanceGuard.Enforce(solution, constraints);

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var seniorHours = OvertimeBalanceGuard.CalculateHours(solution, senior, lookup, constraints);
        var juniorHours = OvertimeBalanceGuard.CalculateHours(solution, junior, lookup, constraints);

        var seniorOt = seniorHours - (double)senior.ProductivityRequiredHours!.Value;
        var juniorOt = juniorHours - (double)junior.ProductivityRequiredHours!.Value;

        // اختلاف اضافه‌کاری باید کمتر از یک شیفت (۷ ساعت) باشد
        Assert.True(
            Math.Abs(seniorOt - juniorOt) <= 7.0,
            $"Expected equal overtime in Neutral department; got seniorOt={seniorOt:F1}, juniorOt={juniorOt:F1}");
    }

    [Fact]
    public void OvertimeBalanceGuard_WhenUserHasNoOvertimeConsent_ExcludesFromOvertimeAndTransfersToConsenting()
    {
        // کاربری که OvertimeConsent = false دارد نباید اضافه کار بگیرد و اضافه کار به متقاضیان منتقل می‌شود
        var start = new DateTime(2026, 9, 1);
        var nonConsentingSenior = MakeUser(1, experienceYears: 25, requiredHours: 70, overtimeConsent: false);
        var consentingJunior = MakeUser(2, experienceYears: 2, requiredHours: 70, overtimeConsent: true);

        var constraints = BuildConstraints(start, [nonConsentingSenior, consentingJunior], overtimeDistributionEnabled: true, overtimePreferenceType: 0);

        var solution = new ShiftSolution();
        // کاربر بدون رضایت 15 شیفت (105 ساعت = 35 ساعت مازاد) روی روزهای زوج دارد
        for (var i = 0; i < 30; i += 2)
        {
            solution.AddAssignment(nonConsentingSenior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }
        // کاربر متقاضی 10 شیفت (70 ساعت = دقیقاً موظفی) روی روزهای فرد دارد
        for (var i = 1; i < 21; i += 2)
        {
            solution.AddAssignment(consentingJunior.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        OvertimeBalanceGuard.Enforce(solution, constraints);

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var nonConsentingHours = OvertimeBalanceGuard.CalculateHours(solution, nonConsentingSenior, lookup, constraints);

        Assert.True(
            nonConsentingHours <= (double)nonConsentingSenior.ProductivityRequiredHours!.Value + 2.0,
            $"Non-consenting user must not have overtime; got hours={nonConsentingHours}, required={nonConsentingSenior.ProductivityRequiredHours}");
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void DepartmentSchedulingSettingsService_ValidatesOvertimePreferenceType(int type, bool expectedValid)
    {
        var dto = new DepartmentSchedulingSettingsDtoAdd
        {
            DepartmentId = 1,
            OvertimePreferenceType = type,
            OvertimeDistributionWeight = 1.0
        };

        var isValid = DepartmentSchedulingSettingsService.ValidateSettings(dto, out var message);

        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
        {
            Assert.Contains("نوع تنظیمات اضافه کار باید بین 0 تا 2 باشد", message);
        }
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(2.5, true)]
    [InlineData(10.0, true)]
    [InlineData(11.0, false)]
    public void DepartmentSchedulingSettingsService_ValidatesOvertimeSeniorityDistributionSlope(double slope, bool expectedValid)
    {
        var dto = new DepartmentSchedulingSettingsDtoAdd
        {
            DepartmentId = 1,
            OvertimePreferenceType = 1,
            OvertimeDistributionWeight = 1.0,
            OvertimeSeniorityDistributionSlope = slope
        };

        var isValid = DepartmentSchedulingSettingsService.ValidateSettings(dto, out var message);

        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
        {
            Assert.Contains("شیب توزیع اضافه کار بر اساس سابقه باید بین 0.1 تا 10 باشد", message);
        }
    }

    [Fact]
    public void OvertimeSeniorityDistributionSlope_SteeperSlope_ProducesGreaterDivergenceInWeight()
    {
        // در شیب تندتر (مثلاً 2.0)، نسبت وزن اضافه کار بین پرسنل کم‌سابقه (3 سال) و باسابقه (13 سال) به مراتب بیشتر از شیب ملایم (0.5) است
        var weightJuniorMild = ShiftSeniorityDistributionGuard.ResolveOvertimeWeight(3, 1, 0.5);
        var weightSeniorMild = ShiftSeniorityDistributionGuard.ResolveOvertimeWeight(13, 1, 0.5);
        var ratioMild = weightJuniorMild / weightSeniorMild;

        var weightJuniorSteep = ShiftSeniorityDistributionGuard.ResolveOvertimeWeight(3, 1, 2.0);
        var weightSeniorSteep = ShiftSeniorityDistributionGuard.ResolveOvertimeWeight(13, 1, 2.0);
        var ratioSteep = weightJuniorSteep / weightSeniorSteep;

        Assert.True(
            ratioSteep > ratioMild * 2.0,
            $"Steeper slope must produce much greater ratio; ratioSteep={ratioSteep:F2}, ratioMild={ratioMild:F2}");
    }

    [Fact]
    public void OvertimeBalanceGuard_EqualSeniorityStaff_BalancesOvertimeEqually()
    {
        // دو کاربر با سابقه یکسان (مثلاً هر دو ۲ سال سابقه) در صورت داشتن اضافه کار نامتوازن، باید به توازن برسند
        var start = new DateTime(2026, 9, 1);
        var peer1 = MakeUser(1, experienceYears: 2, requiredHours: 70, overtimeConsent: true);
        var peer2 = MakeUser(2, experienceYears: 2, requiredHours: 70, overtimeConsent: true);

        var constraints = BuildConstraints(start, [peer1, peer2], overtimeDistributionEnabled: true, overtimePreferenceType: 1);

        var solution = new ShiftSolution();
        // کاربر ۱ دارای ۱۶ شیفت (۱۱۲ ساعت = ۴۲ ساعت اضافه کار)
        for (var i = 0; i < 32; i += 2)
        {
            solution.AddAssignment(peer1.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }
        // کاربر ۲ دارای ۱۰ شیفت (۷۰ ساعت = ۰ ساعت اضافه کار)
        for (var i = 1; i < 21; i += 2)
        {
            solution.AddAssignment(peer2.UserId, 1, start.AddDays(i), ShiftLabel.Morning, false);
        }

        OvertimeBalanceGuard.Enforce(solution, constraints);

        var lookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
        var hours1 = OvertimeBalanceGuard.CalculateHours(solution, peer1, lookup, constraints);
        var hours2 = OvertimeBalanceGuard.CalculateHours(solution, peer2, lookup, constraints);

        var ot1 = hours1 - (double)peer1.ProductivityRequiredHours!.Value;
        var ot2 = hours2 - (double)peer2.ProductivityRequiredHours!.Value;

        // اختلاف اضافه‌کاری بین دو کاربر هم‌سابقه نباید از یک شیفت (۷ ساعت) بیشتر باشد
        Assert.True(
            Math.Abs(ot1 - ot2) <= 7.0,
            $"Expected balanced overtime between peer staff; got ot1={ot1:F1}, ot2={ot2:F1}, diff={Math.Abs(ot1 - ot2):F1}");
    }

    private static ShiftConstraints BuildConstraints(
        DateTime start,
        List<UserConstraint> users,
        bool overtimeDistributionEnabled,
        int overtimePreferenceType,
        double overtimeSeniorityDistributionSlope = 1.0)
    {
        return new ShiftConstraints
        {
            StartDate = start,
            EndDate = start.AddDays(35),
            UserConstraints = users,
            ShiftRequirements =
            [
                new ShiftRequirement
                {
                    ShiftId = 1,
                    ShiftLabel = ShiftLabel.Morning,
                    DepartmentId = 1,
                    DurationHours = 7,
                    SpecialtyRequirements =
                    [
                        new SpecialtyRequirement { SpecialtyId = 10, RequiredTotalCount = 1 }
                    ]
                }
            ],
            HardRules = new HardRuleSet
            {
                EnforceSpecialtyCapacity = true,
                EnforceMaxShiftsPerDay = true,
                ForbidDuplicateDailyAssignments = true,
                EnforceProductivityHours = true
            },
            GlobalConstraints = new GlobalConstraints { MaxShiftsPerDay = 1 },
            SoftWeights = SoftRuleWeights.CreateDefault(),
            EnableOvertimeDistributionBySeniority = overtimeDistributionEnabled,
            OvertimePreferenceType = overtimePreferenceType,
            SeniorityDistributionSlope = 1.0,
            OvertimeSeniorityDistributionSlope = overtimeSeniorityDistributionSlope
        };
    }

    private static UserConstraint MakeUser(int id, int experienceYears, decimal requiredHours, bool overtimeConsent) => new()
    {
        UserId = id,
        UserName = $"u{id}",
        Gender = id % 2 == 0 ? UserGender.Female : UserGender.Male,
        SpecialtyId = 10,
        IsActive = true,
        ShiftType = ShiftTypes.RotatingShift,
        ShiftSubType = ShiftSubTypes.ThreeShifts,
        AllowedShiftLabels = [ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night],
        ExperienceYears = experienceYears,
        IncludedInProductivityPlan = true,
        ProductivityRequiredHours = requiredHours,
        OvertimeConsent = overtimeConsent,
        MaxMonthlyOvertimeHours = 80,
        MaxConsecutiveShifts = 30,
        MinRestDaysBetweenShifts = 0,
        MaxShiftsPerWeek = 7,
        MinDaysBetweenNightShifts = 1
    };
}
