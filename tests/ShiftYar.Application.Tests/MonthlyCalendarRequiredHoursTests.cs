using System;
using System.Collections.Generic;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ProductivityModel.Services;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftModel;
using Xunit;

namespace ShiftYar.Application.Tests
{
    /// <summary>
    /// تست‌های جامع اعتبارسنجی متدولوژی محاسبه ساعت موظفی تقویمی ماهانه پرسنل بالینی
    /// بر اساس تقویم، قانون کار ۴۴ ساعت و کسر ساعت قانون ارتقای بهره‌وری
    /// </summary>
    public class MonthlyCalendarRequiredHoursTests
    {
        private readonly WorkingHoursCalculator _calculator = new();

        #region سناریو ۱: ماه ۳۰ روزه با ۴ جمعه و ۲ تعطیل رسمی وسط هفته (۲۴ روز کاری)

        [Fact]
        public void Scenario1_ThirtyDaysMonth_24WorkingDays_ZeroReduction_Yields176GrossAndNet()
        {
            // ۲۴ روز کاری = ۳۰ منهای (۴ جمعه + ۲ تعطیل رسمی وسط هفته)
            var user = new User
            {
                Id = 101,
                FullName = "پرسنل بدون سابقه و ثابت روز",
                DateOfEmployment = DateTime.UtcNow, // بدو خدمت (۱ ساعت سنوات)
                IncludedProductivityPlan = false,  // غیرمشمول برای تست موظفی خالص = خام
                ShiftType = ShiftEnums.ShiftTypes.FixedShift
            };

            var result = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                user,
                totalDaysInMonth: 30,
                workingDaysCount: 24);

            // ساعت کار پایه هر روز = 44 / 6 = 7.3333333333333333333333333333
            // GrossMonthlyHours = 24 * (44 / 6) = 176.00m
            Assert.Equal(30, result.TotalDaysInMonth);
            Assert.Equal(24, result.WorkingDaysCount);
            Assert.Equal(176.00m, result.GrossMonthlyHours);
            Assert.Equal(0.0m, result.WeeklyProductivityReduction);
            Assert.Equal(0.0m, result.TotalMonthlyReduction);
            Assert.Equal(176.00m, result.NetMonthlyRequiredHours);
        }

        [Fact]
        public void Scenario1_ThirtyDaysMonth_24WorkingDays_MaxEightHoursReduction_Yields141_71Net()
        {
            // کاربر با حداکثر کسر ساعت ۸ ساعت در هفته:
            // سابقه ۱۶+ سال (۵ ساعت) + صعوبت کار (۲ ساعت) + نوبت‌کاری گردشی (۱ ساعت) = ۸ ساعت
            var user = new User
            {
                Id = 102,
                FullName = "سرپرستار باسابقه در گردش",
                DateOfEmployment = DateTime.UtcNow.AddYears(-20),
                HardshipPercent = 100m,
                IsSupervisor = true,
                ShiftType = ShiftEnums.ShiftTypes.RotatingShift,
                ShiftSubType = ShiftEnums.ShiftSubTypes.ThreeShifts,
                IncludedProductivityPlan = true
            };

            var result = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                user,
                totalDaysInMonth: 30,
                workingDaysCount: 24);

            // تخفیف هفتگی: ۵ + ۲ + ۱ = ۸ ساعت در هفته
            Assert.Equal(8.0m, result.WeeklyProductivityReduction);

            // GrossMonthlyHours = 24 * (44 / 6) = 176.00m
            Assert.Equal(176.00m, result.GrossMonthlyHours);

            // نسبت هفته‌های ماه = 30 / 7 = 4.285714...
            Assert.Equal(Math.Round(30m / 7m, 4), result.MonthWeeksFactor);

            // کسر ساعت کل ماه = 8 * (30 / 7) = 34.285714... => گردشده: 34.29 ساعت
            Assert.Equal(Math.Round(8m * (30m / 7m), 2), result.TotalMonthlyReduction);
            Assert.Equal(34.29m, result.TotalMonthlyReduction);

            // موظفی خالص ماهانه = 176.00 - 34.29 = 141.71 ساعت
            Assert.Equal(141.71m, result.NetMonthlyRequiredHours);
        }

        #endregion

        #region سناریو ۲: ماه ۳۱ روزه بدون هیچ تعطیل رسمی غیر از جمعه‌ها (سقف موظفی خام حدود ۱۹۰.۶۷ ساعت)

        [Fact]
        public void Scenario2_ThirtyOneDaysMonth_5Fridays_ZeroHolidays_GrossIs190_67()
        {
            // ماه ۳۱ روزه با ۵ جمعه و بدون تعطیل رسمی وسط هفته:
            // تعداد روزهای کاری = ۳۱ - ۵ = ۲۶ روز کاری
            // موظفی خام = 26 * (44 / 6) = 190.6666... => گردشده: 190.67 ساعت
            var userZero = new User
            {
                Id = 201,
                FullName = "پرسنل بدون کسر ساعت در ماه ۳۱ روزه",
                IncludedProductivityPlan = false
            };

            var resultZero = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                userZero,
                totalDaysInMonth: 31,
                workingDaysCount: 26);

            Assert.Equal(31, resultZero.TotalDaysInMonth);
            Assert.Equal(26, resultZero.WorkingDaysCount);
            Assert.Equal(190.67m, resultZero.GrossMonthlyHours);
            Assert.Equal(0m, resultZero.WeeklyProductivityReduction);
            Assert.Equal(0m, resultZero.TotalMonthlyReduction);
            Assert.Equal(190.67m, resultZero.NetMonthlyRequiredHours);
        }

        [Fact]
        public void Scenario2_ThirtyOneDaysMonth_5Fridays_MaxEightHoursReduction_NetIs155_24()
        {
            // ماه ۳۱ روزه با ۲۶ روز کاری و ۸ ساعت کسر هفتگی:
            // کسر ماهانه = 8 * (31 / 7) = 35.42857... => 35.43 ساعت
            // خالص = 190.67 - 35.43 = 155.24 ساعت
            var userMax = new User
            {
                Id = 202,
                FullName = "پرسنل با حداکثر کسر ساعت در ماه ۳۱ روزه",
                DateOfEmployment = DateTime.UtcNow.AddYears(-18), // ۵ ساعت سنوات
                HardshipPercent = 100m,                          // ۲ ساعت صعوبت
                ShiftType = ShiftEnums.ShiftTypes.RotatingShift,
                ShiftSubType = ShiftEnums.ShiftSubTypes.ThreeShifts, // ۱ ساعت نوبت‌کاری
                IncludedProductivityPlan = true
            };

            var resultMax = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                userMax,
                totalDaysInMonth: 31,
                workingDaysCount: 26);

            Assert.Equal(8.0m, resultMax.WeeklyProductivityReduction);
            Assert.Equal(190.67m, resultMax.GrossMonthlyHours);
            Assert.Equal(35.43m, resultMax.TotalMonthlyReduction);
            Assert.Equal(155.24m, resultMax.NetMonthlyRequiredHours);
        }

        #endregion

        #region سناریو ۳: مقایسه موظفی نهایی فرد دارای ۰ ساعت کسر در برابر فرد دارای حداکثر ۸ ساعت کسر

        [Theory]
        [InlineData(30, 24, 176.00, 34.29, 141.71)]
        [InlineData(31, 26, 190.67, 35.43, 155.24)]
        [InlineData(29, 23, 168.67, 33.14, 135.53)] // ماه اسفند ۲۹ روزه با ۲۳ روز کاری
        public void Scenario3_CompareZeroReductionVsMaxEightReduction_DifferenceEqualsMonthlyReduction(
            int totalDays,
            int workingDays,
            decimal expectedGross,
            decimal expectedMaxReduction,
            decimal expectedNetForMax)
        {
            var userZero = new User
            {
                Id = 301,
                FullName = "پرسنل بدون کسر بهره‌وری",
                IncludedProductivityPlan = false
            };

            var userMax = new User
            {
                Id = 302,
                FullName = "پرسنل با سقف کسر بهره‌وری (۸ ساعت)",
                DateOfEmployment = DateTime.UtcNow.AddYears(-20),
                HardshipPercent = 100m,
                ShiftType = ShiftEnums.ShiftTypes.RotatingShift,
                ShiftSubType = ShiftEnums.ShiftSubTypes.ThreeShifts,
                IncludedProductivityPlan = true
            };

            var resultZero = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(userZero, totalDays, workingDays);
            var resultMax = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(userMax, totalDays, workingDays);

            Assert.Equal(expectedGross, resultZero.GrossMonthlyHours);
            Assert.Equal(expectedGross, resultZero.NetMonthlyRequiredHours);
            Assert.Equal(0.0m, resultZero.WeeklyProductivityReduction);

            Assert.Equal(expectedGross, resultMax.GrossMonthlyHours);
            Assert.Equal(8.0m, resultMax.WeeklyProductivityReduction);
            Assert.Equal(expectedMaxReduction, resultMax.TotalMonthlyReduction);
            Assert.Equal(expectedNetForMax, resultMax.NetMonthlyRequiredHours);

            // اختلاف دقیقاً باید برابر با میزان کسر ساعت ماهانه باشد
            var difference = resultZero.NetMonthlyRequiredHours - resultMax.NetMonthlyRequiredHours;
            Assert.Equal(expectedMaxReduction, difference);
        }

        #endregion

        #region سناریو ۴: پرسنل غیرمشمول قانون ارتقای بهره‌وری

        [Fact]
        public void Scenario4_ExcludedPersonnel_ReceivesZeroReduction_NetEqualsGross()
        {
            // حتی با سابقه ۲۰ ساله، سختی کار ۱۰۰٪ و شیفت گردشی:
            // اگر IncludedProductivityPlan = false باشد، هیچ تخفیفی تعلق نمی‌گیرد.
            var userExcluded = new User
            {
                Id = 401,
                FullName = "پرسنل اداری / غیرمشمول بهره‌وری",
                DateOfEmployment = DateTime.UtcNow.AddYears(-20),
                HardshipPercent = 100m,
                ShiftType = ShiftEnums.ShiftTypes.RotatingShift,
                IncludedProductivityPlan = false
            };

            var reduction = _calculator.GetWeeklyProductivityReduction(userExcluded);
            Assert.Equal(0.0m, reduction);

            var monthly = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                userExcluded,
                totalDaysInMonth: 30,
                workingDaysCount: 24);

            Assert.False(monthly.IsIncludedInProductivityPlan);
            Assert.Equal(0.0m, monthly.WeeklyProductivityReduction);
            Assert.Equal(0.0m, monthly.TotalMonthlyReduction);
            Assert.Equal(monthly.GrossMonthlyHours, monthly.NetMonthlyRequiredHours);
        }

        #endregion

        #region سناریو ۵: رده‌های مدیریت بالینی (سوپروایزر، سرپرستار، مترون)

        [Theory]
        [InlineData(true, false, false, "سوپروایزر")]
        [InlineData(false, true, false, "سرپرستار")]
        [InlineData(false, false, true, "مترون")]
        public void Scenario5_ClinicalManagementRoles_AutomaticallyReceiveTwoHoursHardshipReduction(
            bool isSupervisor,
            bool isHeadNurse,
            bool isMetronInTitle,
            string roleDescription)
        {
            var user = new User
            {
                Id = 501,
                FullName = $"مسئول {roleDescription}",
                JobTitle = isMetronInTitle ? "مترون بیمارستان" : "کارشناس پرستاری",
                IsSupervisor = isSupervisor,
                IsHeadNurse = isHeadNurse,
                DateOfEmployment = DateTime.UtcNow.AddYears(-2), // ۰ تا ۴ سال سابقه = ۱ ساعت
                HardshipPercent = null, // بدون ثبت درصد سختی کار
                HardshipScore = null,   // بدون ثبت امتیاز سختی کار
                ShiftType = ShiftEnums.ShiftTypes.FixedShift, // شیفت ثابت روز = ۰ ساعت نوبت‌کاری
                IncludedProductivityPlan = true
            };

            // طبق ماده ۴ دستورالعمل، مدیریت بالینی خودکار ۲.۰ ساعت صعوبت کار می‌گیرد:
            // تخفیف کل = ۱ (سنوات) + ۲ (صعوبت مدیریت) + ۰ (شیفت ثابت) = ۳.۰ ساعت در هفته
            var weeklyReduction = _calculator.GetWeeklyProductivityReduction(user);
            Assert.Equal(3.0m, weeklyReduction);

            var result = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                user,
                totalDaysInMonth: 30,
                workingDaysCount: 24);

            Assert.Equal(1.0m, result.SeniorityReductionPerWeek);
            Assert.Equal(2.0m, result.HardshipReductionPerWeek);
            Assert.Equal(0.0m, result.ShiftPatternReductionPerWeek);
            Assert.Equal(3.0m, result.WeeklyProductivityReduction);

            // کسر ماهانه = 3 * (30 / 7) = 12.8571... => 12.86 ساعت
            // خالص = 176 - 12.86 = 163.14 ساعت
            Assert.Equal(12.86m, result.TotalMonthlyReduction);
            Assert.Equal(163.14m, result.NetMonthlyRequiredHours);
        }

        #endregion

        #region سناریو ۶: عدم شمارش مضاعف تعطیلات رسمی همپوشان با جمعه‌ها (Calendar Provider)

        [Fact]
        public void Scenario6_CalendarProvider_HolidayOnFriday_IsNotDoubleCounted()
        {
            var provider = new CalendarHolidayProvider();

            // یک ماه شمسی مشخص (مثلاً ماه اول سال ۱۴۰۳ با ۳۱ روز)
            // تعریف تعطیلات فرضی: یکی وسط هفته (شنبه)، یکی روز جمعه
            var (start, end, daysInMonth, isPersian) = provider.GetMonthBounds(1403, 1);
            Assert.True(isPersian);
            Assert.Equal(31, daysInMonth);

            // پیدا کردن اولین جمعه ماه
            var firstFriday = start;
            while (firstFriday.DayOfWeek != DayOfWeek.Friday)
            {
                firstFriday = firstFriday.AddDays(1);
            }

            // یک روز غیرجمعه (مثلاً یکشنبه بعد از اولین جمعه)
            var midWeekHoliday = firstFriday.AddDays(2);

            var customHolidays = new HashSet<DateTime>
            {
                firstFriday,      // تعطیل رسمی واقع در جمعه (نباید دوبار کسر شود)
                midWeekHoliday    // تعطیل رسمی وسط هفته
            };

            var monthInfo = provider.GetMonthWorkingDaysInfo(1403, 1, customHolidays);

            // جمعه در MidWeekOfficialHolidaysCount نباید اضافه شود
            Assert.Contains(firstFriday, monthInfo.FridayDates);
            Assert.DoesNotContain(firstFriday, monthInfo.MidWeekOfficialHolidayDates);
            Assert.Contains(midWeekHoliday, monthInfo.MidWeekOfficialHolidayDates);
            Assert.Equal(1, monthInfo.MidWeekOfficialHolidaysCount);

            // روزهای کاری باید برابر با: TotalDays - (FridaysCount + 1) باشد
            Assert.Equal(31 - (monthInfo.FridaysCount + 1), monthInfo.WorkingDaysCount);
        }

        #endregion

        #region سناریو ۷: یکپارچگی با موتور شبیه‌سازی (Simulated Annealing) و UserConstraint

        [Fact]
        public void Scenario7_ProductivityRequiredHoursResolver_AppliesMonthlyCalendarToUserConstraint()
        {
            var user = new User
            {
                Id = 701,
                FullName = "پرسنل بخش بالینی",
                DateOfEmployment = DateTime.UtcNow.AddYears(-6), // ۲ ساعت سنوات
                HardshipPercent = 50m,                          // ۱ ساعت صعوبت
                ShiftType = ShiftEnums.ShiftTypes.RotatingShift,
                ShiftSubType = ShiftEnums.ShiftSubTypes.ThreeShifts, // ۱ ساعت نوبت‌کاری
                IncludedProductivityPlan = true
            };

            // تخفیف هفتگی: ۲ + ۱ + ۱ = ۴ ساعت
            // ماه ۳۰ روزه با ۲۴ روز کاری: Gross = 176, Reduction = 4 * (30/7) = 17.14, Net = 158.86
            var calendarResult = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(user, 30, 24);
            Assert.Equal(4.0m, calendarResult.WeeklyProductivityReduction);
            Assert.Equal(17.14m, calendarResult.TotalMonthlyReduction);
            Assert.Equal(158.86m, calendarResult.NetMonthlyRequiredHours);

            var constraint = new UserConstraint { UserId = user.Id.Value };

            // اعمال نتیجه تقویمی بر روی قیود الگوریتم تبرید شبیه‌سازی‌شده (Simulated Annealing)
            ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, constraint, calendarResult);

            Assert.True(constraint.IncludedInProductivityPlan);
            Assert.Equal(158.86m, constraint.ProductivityRequiredHours);
            Assert.NotNull(constraint.MonthlyCalendarSnapshot);
            Assert.Equal(158.86m, constraint.MonthlyCalendarSnapshot.NetMonthlyRequiredHours);
            Assert.Equal(176.00m, constraint.MonthlyCalendarSnapshot.GrossMonthlyHours);
            Assert.Equal(4.0m, constraint.MonthlyCalendarSnapshot.WeeklyProductivityReduction);
        }

        [Fact]
        public void Scenario7_ProductivityRequiredHoursResolver_ManualOverridePreemptsCalculatedCalendar()
        {
            // در صورتی که برای کاربر ساعت موظفی دستی تنظیم شده باشد (MaxProductivityRequiredHours)
            var user = new User
            {
                Id = 702,
                FullName = "پرسنل با ساعت دستی",
                MaxProductivityRequiredHours = 120m, // ساعت دستی
                IncludedProductivityPlan = true
            };

            var calendarResult = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(user, 30, 24);
            var constraint = new UserConstraint { UserId = user.Id.Value };

            ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, constraint, calendarResult);

            // قید الگوریتم باید ساعت دستی را مبنا قرار دهد
            Assert.Equal(120m, constraint.ProductivityRequiredHours);
            Assert.True(constraint.MonthlyCalendarSnapshot!.HasManualOverride);
            Assert.Equal(120m, constraint.MonthlyCalendarSnapshot.ManualOverrideHours);
        }

        #endregion

        #region سناریو ۸: تضمین نوع داده Decimal و دقت ۲ رقم اعشار

        [Fact]
        public void Scenario8_CalculationUsesDecimalAndAccurateRounding()
        {
            // بررسی دقیق حاصل ضرب اعشاری بدون افت شناور (Floating Point Inaccuracy)
            const decimal baseDaily = 44.0m / 6.0m;
            Assert.Equal(7.3333333333333333333333333333m, baseDaily);

            var user = new User
            {
                Id = 801,
                FullName = "تست دقت اعشار",
                DateOfEmployment = DateTime.UtcNow.AddYears(-1), // ۱ ساعت
                HardshipPercent = 25m,                          // ۰.۵ ساعت
                ShiftType = ShiftEnums.ShiftTypes.FixedShift,   // ۰ ساعت
                IncludedProductivityPlan = true
            };

            // تخفیف هفتگی: ۱.۵ ساعت
            var result = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(user, 31, 25);

            // Gross = 25 * (44 / 6) = 183.33333333333333333333333333m => 183.33m
            Assert.Equal(183.33m, result.GrossMonthlyHours);

            // TotalReduction = 1.5 * (31 / 7) = 6.6428571428571428571428571429m => 6.64m
            Assert.Equal(6.64m, result.TotalMonthlyReduction);

            // Net = 183.33 - 6.64 = 176.69m
            Assert.Equal(176.69m, result.NetMonthlyRequiredHours);
        }

        #endregion

        #region سناریو ۹: تست کاربر جواد انصاری در مهر ماه ۱۴۰۵ (۳۰ روزه، ۴ جمعه، بدون تعطیل رسمی وسط هفته)

        [Fact]
        public void Scenario9_Mehr1405_JavadAnsari_WithHardshipThirtyPercent_Calculates183Hours()
        {
            // جواد انصاری: ۲ سال سابقه (۱ ساعت سنوات) + ۳۰٪ سختی کار (۱ ساعت صعوبت) + شیفت ثابت روز (۰ ساعت نوبت‌کاری)
            // تخفیف هفتگی: ۲.۰ ساعت
            // مهر ماه ۱۴۰۵: ۳۰ روزه، ۴ جمعه، ۰ تعطیل رسمی وسط هفته => ۲۶ روز کاری
            // موظفی خام: ۲۶ * (۴۴/۶) = ۱۹۰.۶۷ ساعت
            // کسر ماهانه بر مبنای ۴ هفته استاندارد: ۲ * ۴ = ۸.۰ ساعت
            // موظفی خالص: ۱۹۰.۶۷ - ۸.۰ = ۱۸۲.۶۷ ساعت => گردشده: ۱۸۳ ساعت
            var user = new User
            {
                Id = 2,
                FullName = "جواد انصاری",
                DateOfEmployment = DateTime.UtcNow.AddYears(-2), // حدود ۲ سال سابقه = ۱ ساعت کسر سنوات
                HardshipPercent = 30m,                           // ۳۰ درصد سختی کار نظام هماهنگ (بازه ۲۶ تا ۵۰ درصد = ۱.۰ ساعت)
                HardshipScore = null,
                ShiftType = ShiftEnums.ShiftTypes.FixedShift,    // ثابت صبح = ۰ ساعت نوبت‌کاری
                IncludedProductivityPlan = true
            };

            var weeklyReduction = _calculator.GetWeeklyProductivityReduction(user);
            Assert.Equal(2.0m, weeklyReduction);

            var result = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                user,
                totalDaysInMonth: 30,
                workingDaysCount: 26,
                numberOfWeeksInMonth: 4);

            Assert.Equal(30, result.TotalDaysInMonth);
            Assert.Equal(26, result.WorkingDaysCount);
            Assert.Equal(190.67m, result.GrossMonthlyHours);
            Assert.Equal(1.0m, result.SeniorityReductionPerWeek);
            Assert.Equal(1.0m, result.HardshipReductionPerWeek);
            Assert.Equal(0.0m, result.ShiftPatternReductionPerWeek);
            Assert.Equal(2.0m, result.WeeklyProductivityReduction);
            Assert.Equal(8.0m, result.TotalMonthlyReduction);
            Assert.Equal(182.67m, result.NetMonthlyRequiredHours);
            Assert.Equal(183, result.NetMonthlyRequiredHoursRounded);
        }

        [Fact]
        public void Scenario9_Mehr1405_JavadAnsari_WithoutHardship_Calculates187Hours()
        {
            // جواد انصاری بدون ثبت درصد سختی کار: ۲ سال سابقه (۱ ساعت سنوات) + سختی کار (۰ ساعت) + شیفت ثابت (۰ ساعت)
            // تخفیف هفتگی: ۱.۰ ساعت
            // کسر ماهانه: ۱ * ۴ = ۴.۰ ساعت
            // موظفی خالص: ۱۹۰.۶۷ - ۴.۰ = ۱۸۶.۶۷ ساعت => گردشده: ۱۸۷ ساعت
            var user = new User
            {
                Id = 2,
                FullName = "جواد انصاری",
                DateOfEmployment = DateTime.UtcNow.AddYears(-2),
                HardshipPercent = null,
                HardshipScore = null,
                ShiftType = ShiftEnums.ShiftTypes.FixedShift,
                IncludedProductivityPlan = true
            };

            var weeklyReduction = _calculator.GetWeeklyProductivityReduction(user);
            Assert.Equal(1.0m, weeklyReduction);

            var result = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                user,
                totalDaysInMonth: 30,
                workingDaysCount: 26,
                numberOfWeeksInMonth: 4);

            Assert.Equal(0.0m, result.HardshipReductionPerWeek);
            Assert.Equal(1.0m, result.WeeklyProductivityReduction);
            Assert.Equal(4.0m, result.TotalMonthlyReduction);
            Assert.Equal(186.67m, result.NetMonthlyRequiredHours);
            Assert.Equal(187, result.NetMonthlyRequiredHoursRounded);
        }

        #endregion

        #region سناریو ۱۰: تضمین کسر ۱ ساعت هفتگی (۴ ساعت ماهانه) برای دو نوبته، سه نوبته و ثابت شب

        [Theory]
        [InlineData(ShiftEnums.ShiftSubTypes.TwoShifts, null, 1.0)]
        [InlineData(ShiftEnums.ShiftSubTypes.ThreeShifts, null, 1.0)]
        [InlineData(ShiftEnums.ShiftSubTypes.FixedNight, null, 1.0)]
        [InlineData(ShiftEnums.ShiftSubTypes.FixedMorning, null, 0.0)]
        [InlineData(ShiftEnums.ShiftSubTypes.FixedEvening, null, 0.0)]
        public void Scenario10_ShiftPatterns_DeductionsMatchPolicy(
            ShiftEnums.ShiftSubTypes subType,
            ShiftEnums.ShiftTypes? shiftType,
            decimal expectedShiftReduction)
        {
            var user = new User
            {
                Id = 1001,
                FullName = "پرسنل تست الگوی شیفت",
                DateOfEmployment = DateTime.UtcNow, // بدو خدمت = ۱ ساعت سنوات
                ShiftSubType = subType,
                ShiftType = shiftType,
                IncludedProductivityPlan = true
            };

            var result = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                user,
                totalDaysInMonth: 30,
                workingDaysCount: 26,
                numberOfWeeksInMonth: 4);

            Assert.Equal(expectedShiftReduction, result.ShiftPatternReductionPerWeek);
            var expectedWeekly = 1.0m + expectedShiftReduction; // سنوات (۱ ساعت) + نوبت‌کاری
            Assert.Equal(expectedWeekly, result.WeeklyProductivityReduction);
            Assert.Equal(expectedWeekly * 4.0m, result.TotalMonthlyReduction);
        }

        [Fact]
        public void Scenario10_FixedNight_ViaAllowedShiftPermissions_ReceivesOneHourReduction()
        {
            var user = new User
            {
                Id = 1002,
                FullName = "پرسنل فیکس شب با مجوز",
                DateOfEmployment = DateTime.UtcNow,
                ShiftType = ShiftEnums.ShiftTypes.FixedShift,
                AllowedShiftPermissions = ShiftEnums.UserShiftPermission.Night,
                IncludedProductivityPlan = true
            };

            var result = _calculator.CalculateMonthlyRequiredHoursForDaysDetails(
                user,
                totalDaysInMonth: 30,
                workingDaysCount: 26,
                numberOfWeeksInMonth: 4);

            Assert.Equal(1.0m, result.ShiftPatternReductionPerWeek);
            Assert.Equal(2.0m, result.WeeklyProductivityReduction); // ۱ سنوات + ۱ ثابت شب
            Assert.Equal(8.0m, result.TotalMonthlyReduction);       // ۲ * ۴ = ۸ ساعت
            Assert.Equal(182.67m, result.NetMonthlyRequiredHours);  // ۱۹۰.۶۷ - ۸ = ۱۸۲.۶۷
            Assert.Equal(183, result.NetMonthlyRequiredHoursRounded);
        }

        #endregion
    }
}
