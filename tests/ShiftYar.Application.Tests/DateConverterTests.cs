using ShiftYar.Application.Common.Utilities;
using System;
using Xunit;

namespace ShiftYar.Application.Tests;

public class DateConverterTests
{
    [Theory]
    [InlineData("1405/06/03", 2026, 8, 25)]
    [InlineData("1404/06/03", 2025, 8, 25)]
    [InlineData("1405/06/31", 2026, 9, 22)]
    [InlineData("1405/01/01", 2026, 3, 21)]
    public void ConvertToGregorianDate_StandardSlashFormat_IsCorrect(string persian, int y, int m, int d)
    {
        var result = DateConverter.ConvertToGregorianDate(persian);

        Assert.Equal(new DateTime(y, m, d), result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
        Assert.Equal(TimeSpan.Zero, result.TimeOfDay);
    }

    [Theory]
    [InlineData("۱۴۰۵/۰۶/۰۳", 2026, 8, 25)]
    [InlineData("1405-06-03", 2026, 8, 25)]
    [InlineData("1405.06.03", 2026, 8, 25)]
    [InlineData(" 1405 / 06 / 03 ", 2026, 8, 25)]
    [InlineData("1405/6/3", 2026, 8, 25)]
    public void ConvertToGregorianDate_AcceptsPersianDigitsAndAlternateSeparators(string persian, int y, int m, int d)
    {
        var result = DateConverter.ConvertToGregorianDate(persian);
        Assert.Equal(new DateTime(y, m, d), result);
    }

    [Fact]
    public void ConvertToPersianDate_RoundTripsWithGregorianConversion()
    {
        var gregorian = DateConverter.ConvertToGregorianDate("1405/06/03");
        var persian = DateConverter.ConvertToPersianDate(gregorian);
        Assert.Equal("1405/06/03", persian);
        Assert.Equal(gregorian, DateConverter.ConvertToGregorianDate(persian));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertToGregorianDate_Empty_ThrowsArgumentException(string? input)
    {
        Assert.ThrowsAny<ArgumentException>(() => DateConverter.ConvertToGregorianDate(input!));
    }

    [Theory]
    [InlineData("1405/06")]
    [InlineData("not-a-date")]
    public void ConvertToGregorianDate_BadFormat_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => DateConverter.ConvertToGregorianDate(input));
    }

    [Theory]
    [InlineData("1405/13/01")]
    [InlineData("1405/06/32")]
    [InlineData("1404/12/30")] // غیر کبیسه: اسفند حداکثر ۲۹ روز
    public void ConvertToGregorianDate_InvalidCalendarDay_Throws(string input)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => DateConverter.ConvertToGregorianDate(input));
    }

    [Fact]
    public void ConvertToGregorianDate_LeapEsfand30_IsValid()
    {
        // ۱۴۰۳ کبیسه است و ۱۲/۳۰ معتبر است
        var result = DateConverter.ConvertToGregorianDate("1403/12/30");
        Assert.Equal(new DateTime(2025, 3, 20), result);
        Assert.Equal("1403/12/30", DateConverter.ConvertToPersianDate(result));
    }
}
