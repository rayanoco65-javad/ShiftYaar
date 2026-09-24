using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ProductivityModel;
using ShiftYar.Domain.Entities.ShiftDateModel;

namespace ShiftYar.Application.Common.Utilities
{
    /// <summary>
    /// پیاده‌سازی سرویس تقویم و استخراج روزهای تعطیل رسمی تقویمی بر مبنای تقویم شمسی/میلادی و دیتابیس ShiftDate.
    /// </summary>
    public class CalendarHolidayProvider : ICalendarHolidayProvider
    {
        private static readonly PersianCalendar PersianCalendar = new();
        private readonly IEfRepository<ShiftDate>? _shiftDateRepository;
        private readonly ILogger<CalendarHolidayProvider>? _logger;

        public CalendarHolidayProvider(
            IEfRepository<ShiftDate>? shiftDateRepository = null,
            ILogger<CalendarHolidayProvider>? logger = null)
        {
            _shiftDateRepository = shiftDateRepository;
            _logger = logger;
        }

        /// <summary>
        /// استخراج مرزهای ماه بر اساس سال و ماه. اگر سال در بازه ۱۲۰۰ تا ۱۶۰۰ باشد، شمسی تلقی می‌شود.
        /// </summary>
        public (DateTime MonthStart, DateTime MonthEnd, int DaysInMonth, bool IsPersian) GetMonthBounds(int year, int month)
        {
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
            }

            var isPersian = year >= 1200 && year <= 1600;
            if (isPersian)
            {
                var daysInMonth = PersianCalendar.GetDaysInMonth(year, month);
                var monthStart = PersianCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0).Date;
                var monthEnd = PersianCalendar.ToDateTime(year, month, daysInMonth, 0, 0, 0, 0).Date;
                return (monthStart, monthEnd, daysInMonth, true);
            }
            else
            {
                var daysInMonth = DateTime.DaysInMonth(year, month);
                var monthStart = new DateTime(year, month, 1).Date;
                var monthEnd = new DateTime(year, month, daysInMonth).Date;
                return (monthStart, monthEnd, daysInMonth, false);
            }
        }

        public async Task<ISet<DateTime>> GetOfficialHolidaysAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var holidays = new HashSet<DateTime>();

            if (_shiftDateRepository != null)
            {
                try
                {
                    var start = startDate.Date;
                    var endExclusive = endDate.Date.AddDays(1);

                    var (dates, _) = await _shiftDateRepository.GetByFilterAsync(
                        new SimpleFilter<ShiftDate>(d =>
                            d.Date != null &&
                            d.Date >= start &&
                            d.Date < endExclusive &&
                            d.IsHoliday == true));

                    foreach (var d in dates)
                    {
                        if (d.Date.HasValue)
                        {
                            holidays.Add(d.Date.Value.Date);
                        }
                    }

                    if (holidays.Count > 0)
                    {
                        return holidays;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load holidays from database, falling back to JSON resources.");
                }
            }

            return LoadHolidaysFromResource(startDate, endDate);
        }

        public ISet<DateTime> GetOfficialHolidays(DateTime startDate, DateTime endDate, int? persianYear = null)
        {
            if (_shiftDateRepository != null)
            {
                try
                {
                    var start = startDate.Date;
                    var endExclusive = endDate.Date.AddDays(1);

                    var task = _shiftDateRepository.GetByFilterAsync(
                        new SimpleFilter<ShiftDate>(d =>
                            d.Date != null &&
                            d.Date >= start &&
                            d.Date < endExclusive &&
                            d.IsHoliday == true));

                    var (dates, _) = task.GetAwaiter().GetResult();
                    var holidays = dates
                        .Where(d => d.Date.HasValue)
                        .Select(d => d.Date!.Value.Date)
                        .ToHashSet();

                    if (holidays.Count > 0)
                    {
                        return holidays;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to query holidays synchronously, falling back to JSON resources.");
                }
            }

            return LoadHolidaysFromResource(startDate, endDate);
        }

        public MonthWorkingDaysInfo GetMonthWorkingDaysInfo(int year, int month, ISet<DateTime>? customOfficialHolidays = null)
        {
            var (start, end, daysInMonth, isPersian) = GetMonthBounds(year, month);
            var holidays = customOfficialHolidays ?? GetOfficialHolidays(start, end, isPersian ? year : null);

            var fridays = new List<DateTime>();
            var midWeekHolidays = new List<DateTime>();

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Friday)
                {
                    fridays.Add(d);
                }
                else if (holidays.Contains(d.Date))
                {
                    // تعطیل رسمی وسط هفته (جمعه‌ها نباید دوبار کسر شوند)
                    midWeekHolidays.Add(d);
                }
            }

            var workingDays = Math.Max(0, daysInMonth - (fridays.Count + midWeekHolidays.Count));

            return new MonthWorkingDaysInfo
            {
                Year = year,
                Month = month,
                IsPersian = isPersian,
                MonthStart = start,
                MonthEnd = end,
                TotalDays = daysInMonth,
                FridaysCount = fridays.Count,
                MidWeekOfficialHolidaysCount = midWeekHolidays.Count,
                WorkingDaysCount = workingDays,
                FridayDates = fridays,
                MidWeekOfficialHolidayDates = midWeekHolidays
            };
        }

        public async Task<MonthWorkingDaysInfo> GetMonthWorkingDaysInfoAsync(
            int year,
            int month,
            ISet<DateTime>? customOfficialHolidays = null,
            CancellationToken cancellationToken = default)
        {
            var (start, end, daysInMonth, isPersian) = GetMonthBounds(year, month);
            var holidays = customOfficialHolidays ?? await GetOfficialHolidaysAsync(start, end, cancellationToken);

            var fridays = new List<DateTime>();
            var midWeekHolidays = new List<DateTime>();

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Friday)
                {
                    fridays.Add(d);
                }
                else if (holidays.Contains(d.Date))
                {
                    midWeekHolidays.Add(d);
                }
            }

            var workingDays = Math.Max(0, daysInMonth - (fridays.Count + midWeekHolidays.Count));

            return new MonthWorkingDaysInfo
            {
                Year = year,
                Month = month,
                IsPersian = isPersian,
                MonthStart = start,
                MonthEnd = end,
                TotalDays = daysInMonth,
                FridaysCount = fridays.Count,
                MidWeekOfficialHolidaysCount = midWeekHolidays.Count,
                WorkingDaysCount = workingDays,
                FridayDates = fridays,
                MidWeekOfficialHolidayDates = midWeekHolidays
            };
        }

        private static HashSet<DateTime> LoadHolidaysFromResource(DateTime startDate, DateTime endDate)
        {
            var holidays = new HashSet<DateTime>();
            try
            {
                var startYear = PersianCalendar.GetYear(startDate);
                var endYear = PersianCalendar.GetYear(endDate);

                for (var py = startYear; py <= endYear; py++)
                {
                    var possiblePaths = new[]
                    {
                        Path.Combine(AppContext.BaseDirectory, "Resources", "Holidays", $"holidays_{py}.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Holidays", $"holidays_{py}.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "ShiftYar.Api", "Resources", "Holidays", $"holidays_{py}.json"),
                        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ShiftYar.Api", "Resources", "Holidays", $"holidays_{py}.json")
                    };

                    var filePath = possiblePaths.FirstOrDefault(File.Exists);
                    if (filePath == null)
                    {
                        continue;
                    }

                    var json = File.ReadAllText(filePath);
                    using var doc = JsonDocument.Parse(json);
                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        if (element.TryGetProperty("Date", out var dateProp))
                        {
                            var pDateStr = dateProp.GetString();
                            if (!string.IsNullOrWhiteSpace(pDateStr))
                            {
                                var parts = pDateStr.Split('/');
                                if (parts.Length == 3 &&
                                    int.TryParse(parts[0], out var y) &&
                                    int.TryParse(parts[1], out var m) &&
                                    int.TryParse(parts[2], out var d))
                                {
                                    try
                                    {
                                        var gDate = PersianCalendar.ToDateTime(y, m, d, 0, 0, 0, 0).Date;
                                        if (gDate >= startDate.Date && gDate <= endDate.Date)
                                        {
                                            holidays.Add(gDate);
                                        }
                                    }
                                    catch
                                    {
                                        // Ignore potential invalid dates in JSON
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fail-safe: returns whatever parsed
            }

            return holidays;
        }
    }
}
