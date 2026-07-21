using ShiftYar.Application.Common.Utilities;
using Xunit;

namespace ShiftYar.Application.Tests;

public class HolidayWeekendNightRulesTests
{
    [Fact]
    public void FridayHoliday_ThursdayNightAndFridayNight_CountAsWeekend()
    {
        var holidays = new HashSet<DateTime> { new(2026, 7, 24) }; // Friday
        var thursday = new DateTime(2026, 7, 23);
        var friday = new DateTime(2026, 7, 24);
        var wednesday = new DateTime(2026, 7, 22);

        Assert.True(HolidayWeekendNightRules.IsHolidayWeekendNight(friday, holidays));
        Assert.True(HolidayWeekendNightRules.IsHolidayWeekendNight(thursday, holidays));
        Assert.False(HolidayWeekendNightRules.IsHolidayWeekendNight(wednesday, holidays));
    }

    [Fact]
    public void ThursdayAndFridayHoliday_WednesdayNightAlsoCounts()
    {
        var holidays = new HashSet<DateTime>
        {
            new(2026, 7, 23), // Thursday
            new(2026, 7, 24)  // Friday
        };
        var wednesday = new DateTime(2026, 7, 22);
        var thursday = new DateTime(2026, 7, 23);
        var friday = new DateTime(2026, 7, 24);
        var tuesday = new DateTime(2026, 7, 21);

        Assert.True(HolidayWeekendNightRules.IsHolidayWeekendNight(wednesday, holidays));
        Assert.True(HolidayWeekendNightRules.IsHolidayWeekendNight(thursday, holidays));
        Assert.True(HolidayWeekendNightRules.IsHolidayWeekendNight(friday, holidays));
        Assert.False(HolidayWeekendNightRules.IsHolidayWeekendNight(tuesday, holidays));
    }
}
