using ShiftYar.Application.Features.ShiftModel.Services;
using System;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class WeeklyAlternatingShiftServiceTests
{
    [Fact]
    public void GetWeekNumber_StartsOnSaturday_CorrectlyNumbersWeeks()
    {
        // شنبه: 2026-03-21 (مثلاً ۱ فروردین یک سال فرضی که شنبه باشد)
        var rangeStart = new DateTime(2026, 3, 21); // شنبه (Saturday)

        // هفته اول: ۲۱ مارس (شنبه) تا ۲۷ مارس (جمعه)
        Assert.Equal(1, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 21), rangeStart));
        Assert.Equal(1, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 23), rangeStart));
        Assert.Equal(1, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 27), rangeStart));

        // هفته دوم: ۲۸ مارس (شنبه) تا ۳ آوریل (جمعه)
        Assert.Equal(2, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 28), rangeStart));
        Assert.Equal(2, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 4, 3), rangeStart));

        // هفته سوم: ۴ آوریل (شنبه)
        Assert.Equal(3, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 4, 4), rangeStart));
    }

    [Fact]
    public void GetWeekNumber_IncompleteFirstWeekStartingOnWednesday_CalculatesCorrectly()
    {
        // فرض کنید ماه در روز چهارشنبه شروع می‌شود (مثلاً ۲۵ مارس ۲۰۲۶)
        var rangeStart = new DateTime(2026, 3, 25); // چهارشنبه (Wednesday)

        // هفته اول (ناقص): چهارشنبه ۲۵ مارس تا جمعه ۲۷ مارس
        Assert.Equal(1, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 25), rangeStart));
        Assert.Equal(1, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 26), rangeStart)); // پنجشنبه
        Assert.Equal(1, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 27), rangeStart)); // جمعه

        // هفته دوم (کامل): شنبه ۲۸ مارس تا جمعه ۳ آوریل
        Assert.Equal(2, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 28), rangeStart)); // شنبه
        Assert.Equal(2, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 29), rangeStart)); // یکشنبه
        Assert.Equal(2, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 4, 3), rangeStart));  // جمعه

        // هفته سوم: شنبه ۴ آوریل
        Assert.Equal(3, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 4, 4), rangeStart)); // شنبه
    }

    [Fact]
    public void GetWeekNumber_IncompleteFirstWeekStartingOnFriday_OnlyOneDayInWeek1()
    {
        // فرض کنید ماه در روز جمعه شروع می‌شود (مثلاً ۲۷ مارس ۲۰۲۶)
        var rangeStart = new DateTime(2026, 3, 27); // جمعه

        // هفته اول: فقط جمعه
        Assert.Equal(1, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 27), rangeStart));

        // شنبه فردا وارد هفته دوم می‌شود
        Assert.Equal(2, WeeklyAlternatingShiftService.GetWeekNumber(new DateTime(2026, 3, 28), rangeStart));
    }

    [Theory]
    [InlineData(1, ShiftLabel.Morning, ShiftLabel.Morning)]
    [InlineData(2, ShiftLabel.Morning, ShiftLabel.Evening)]
    [InlineData(3, ShiftLabel.Morning, ShiftLabel.Morning)]
    [InlineData(4, ShiftLabel.Morning, ShiftLabel.Evening)]
    [InlineData(5, ShiftLabel.Morning, ShiftLabel.Morning)]
    [InlineData(1, ShiftLabel.Evening, ShiftLabel.Evening)]
    [InlineData(2, ShiftLabel.Evening, ShiftLabel.Morning)]
    [InlineData(3, ShiftLabel.Evening, ShiftLabel.Evening)]
    [InlineData(4, ShiftLabel.Evening, ShiftLabel.Morning)]
    [InlineData(5, ShiftLabel.Evening, ShiftLabel.Evening)]
    public void GetAllowedDayShift_AlternatesCorrectlyAcrossWeeks(int weekNumber, ShiftLabel firstWeekShift, ShiftLabel expected)
    {
        var allowed = WeeklyAlternatingShiftService.GetAllowedDayShift(weekNumber, firstWeekShift);
        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void GetOppositeDayShift_MorningAndEvening_InvertCorrectly()
    {
        Assert.Equal(ShiftLabel.Evening, WeeklyAlternatingShiftService.GetOppositeDayShift(ShiftLabel.Morning));
        Assert.Equal(ShiftLabel.Morning, WeeklyAlternatingShiftService.GetOppositeDayShift(ShiftLabel.Evening));
        Assert.Throws<ArgumentException>(() => WeeklyAlternatingShiftService.GetOppositeDayShift(ShiftLabel.Night));
    }

    [Fact]
    public void ValidateDailyShift_NightShift_AlwaysAllowedRegardlessOfAlternating()
    {
        var rangeStart = new DateTime(2026, 3, 21); // شنبه
        var week1Date = new DateTime(2026, 3, 22);

        var result = WeeklyAlternatingShiftService.ValidateDailyShift(
            week1Date,
            ShiftLabel.Night,
            rangeStart,
            isWeeklyAlternatingActive: true,
            firstWeekShift: ShiftLabel.Morning);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateDailyShift_InactiveAlternating_AllowsAllDayShifts()
    {
        var rangeStart = new DateTime(2026, 3, 21);
        var week1Date = new DateTime(2026, 3, 22);

        var morningResult = WeeklyAlternatingShiftService.ValidateDailyShift(
            week1Date,
            ShiftLabel.Morning,
            rangeStart,
            isWeeklyAlternatingActive: false,
            firstWeekShift: ShiftLabel.Evening);

        var eveningResult = WeeklyAlternatingShiftService.ValidateDailyShift(
            week1Date,
            ShiftLabel.Evening,
            rangeStart,
            isWeeklyAlternatingActive: false,
            firstWeekShift: ShiftLabel.Morning);

        Assert.True(morningResult.IsValid);
        Assert.True(eveningResult.IsValid);
    }

    [Fact]
    public void ValidateDailyShift_ActiveAlternating_EnforcesOddAndEvenWeekRules()
    {
        // شروع ماه: شنبه ۲۱ مارس
        var rangeStart = new DateTime(2026, 3, 21);
        var week1Date = new DateTime(2026, 3, 23); // هفته ۱
        var week2Date = new DateTime(2026, 3, 30); // هفته ۲

        // شروع با شیفت صبح: هفته ۱ باید صبح باشد، هفته ۲ باید عصر باشد
        var week1Morning = WeeklyAlternatingShiftService.ValidateDailyShift(
            week1Date, ShiftLabel.Morning, rangeStart, true, ShiftLabel.Morning);
        Assert.True(week1Morning.IsValid);
        Assert.Equal(ShiftLabel.Morning, week1Morning.AllowedDayShift);
        Assert.Equal(1, week1Morning.WeekNumber);

        // تلاش برای انتساب عصر در هفته ۱ -> نامعتبر
        var week1Evening = WeeklyAlternatingShiftService.ValidateDailyShift(
            week1Date, ShiftLabel.Evening, rangeStart, true, ShiftLabel.Morning);
        Assert.False(week1Evening.IsValid);
        Assert.NotNull(week1Evening.ErrorMessage);
        Assert.Contains("هفته 1", week1Evening.ErrorMessage);
        Assert.Contains("عصر", week1Evening.ErrorMessage);
        Assert.Contains("صبح", week1Evening.ErrorMessage);

        // هفته ۲: باید عصر باشد
        var week2Evening = WeeklyAlternatingShiftService.ValidateDailyShift(
            week2Date, ShiftLabel.Evening, rangeStart, true, ShiftLabel.Morning);
        Assert.True(week2Evening.IsValid);
        Assert.Equal(ShiftLabel.Evening, week2Evening.AllowedDayShift);
        Assert.Equal(2, week2Evening.WeekNumber);

        // تلاش برای انتساب صبح در هفته ۲ -> نامعتبر
        var week2Morning = WeeklyAlternatingShiftService.ValidateDailyShift(
            week2Date, ShiftLabel.Morning, rangeStart, true, ShiftLabel.Morning);
        Assert.False(week2Morning.IsValid);
        Assert.Contains("هفته 2", week2Morning.ErrorMessage);
    }

    [Fact]
    public void ValidateDailyShift_InvalidFirstWeekShift_ReturnsError()
    {
        var rangeStart = new DateTime(2026, 3, 21);
        var date = new DateTime(2026, 3, 22);

        var result = WeeklyAlternatingShiftService.ValidateDailyShift(
            date, ShiftLabel.Morning, rangeStart, true, ShiftLabel.Night);

        Assert.False(result.IsValid);
        Assert.Contains("صبح یا عصر", result.ErrorMessage);
    }
}
