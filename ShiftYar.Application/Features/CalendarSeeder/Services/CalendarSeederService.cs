using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Features.CalendarSeeder.Filters;
using ShiftYar.Application.Interfaces.CalendarSeeder;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Application.Interfaces.IFileSystem;
using System.Globalization;
using System.Text.Json;
using ShiftYar.Application.Common.Models.ResponseModel;
using static System.Runtime.InteropServices.JavaScript.JSType;
using ShiftYar.Application.DTOs.DepartmentModel;

namespace ShiftYar.Application.Features.CalendarSeeder.Services
{
    public class CalendarSeederService : ICalendarSeederService
    {
        private readonly IEfRepository<ShiftDate> _shiftDateRepository;
        private readonly ILogger<CalendarSeederService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IFileSystemService _fileSystem;

        public CalendarSeederService(IEfRepository<ShiftDate> shiftDateRepository, ILogger<CalendarSeederService> logger, IHttpContextAccessor httpContextAccessor, IFileSystemService fileSystem)
        {
            _shiftDateRepository = shiftDateRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _fileSystem = fileSystem;
        }

        public async Task<ApiResponse<PagedResponse<ShiftDate>>> GetDatesAsync(ShiftDateFilter filter)
        {
            try
            {
                _logger.LogInformation("Fetching dates with filters: {@Filter}", filter);
                var result = await _shiftDateRepository.GetByFilterAsync(filter);

                var pagedResponse = new PagedResponse<ShiftDate>
                {
                    Items = result.Items,
                    TotalCount = result.TotalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
                };

                _logger.LogInformation("Successfully fetched {Count} departments out of {TotalCount}", result.Items.Count, result.TotalCount);
                return ApiResponse<PagedResponse<ShiftDate>>.Success(pagedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching dates");
                return ApiResponse<PagedResponse<ShiftDate>>.Fail("خطا در دریافت لیست تاریخ‌ها: " + ex.Message);
            }
        }

        public async Task SeedShiftDatesAsync(int year)
        {
            try
            {
                // مسیر فایل JSON تعطیلات (مثلاً holidays_1403.json)
                string path = _fileSystem.CombinePath("Resources", "Holidays", $"holidays_{year}.json");

                if (!_fileSystem.FileExists(path))
                    throw new FileNotFoundException($"Holiday file not found for year {year}.");

                // خواندن JSON و تبدیل به لیست تعطیلات
                var json = await _fileSystem.ReadAllTextAsync(path);
                var holidays = JsonSerializer.Deserialize<List<HolidayEntry>>(json)!;

                var shiftDates = new List<ShiftDate>();
                var persianCalendar = new PersianCalendar();

                // سال 1404/01/01 شمسی رو به معادل میلادی تبدیل کن
                var startDate = persianCalendar.ToDateTime(year, 1, 1, 0, 0, 0, 0);
                var endDate = persianCalendar.ToDateTime(year, 12, 29, 0, 0, 0, 0); // فرض: سال ۱۲ ماه دارد، ماه ۱۲ حداکثر ۲۹ روزه

                // اما بعضی سال‌ها (سال کبیسه)، ماه ۱۲، ۳۰ روزه‌ست
                if (persianCalendar.IsLeapYear(year))
                {
                    endDate = persianCalendar.ToDateTime(year, 12, 30, 0, 0, 0, 0);
                }

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    // تاریخ شمسی
                    string persianDate = $"{persianCalendar.GetYear(date):0000}/{persianCalendar.GetMonth(date):00}/{persianCalendar.GetDayOfMonth(date):00}";

                    // بررسی آیا این تاریخ تعطیل است
                    var match = holidays.FirstOrDefault(h => h.Date == persianDate);

                    // نام روز هفته (مثلاً شنبه، یکشنبه و ...)
                    string dayOfWeekTitle = GetPersianDayOfWeekTitle(date.DayOfWeek);
                    // جمعه‌ها رو به‌طور پیش‌فرض تعطیل بدون
                    bool isFriday = date.DayOfWeek == DayOfWeek.Friday;

                    // ایجاد شیفت‌تاریخ جدید
                    var shiftDate = new ShiftDate
                    {
                        Date = date.Date,
                        PersianDate = persianDate,
                        DayTitle = dayOfWeekTitle,
                        IsHoliday = match != null || isFriday,
                        HolidayEvent = match?.Title ?? (date.DayOfWeek == DayOfWeek.Friday ? "تعطیل هفتگی" : null),
                        CreateDate = DateTime.Now,
                        UpdateDate = null,
                        TheUserId = null // یا یک مقدار ثابت در صورت نیاز
                    };

                    await _shiftDateRepository.AddAsync(shiftDate);
                }

                await _shiftDateRepository.SaveAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس بروزرسانی تقویم با خطا مواجه شد: " + ex.Message);
            }
        }

        public async Task SetAsHolidayAsync(string persianDate, string holidayEvent)
        {
            try
            {
                // تبدیل تاریخ شمسی به میلادی
                var dateParts = persianDate.Split('/');
                if (dateParts.Length != 3)
                    throw new Exception("فرمت تاریخ شمسی نامعتبر است. فرمت صحیح: yyyy/MM/dd");

                var persianCalendar = new PersianCalendar();
                var year = int.Parse(dateParts[0]);
                var month = int.Parse(dateParts[1]);
                var day = int.Parse(dateParts[2]);

                var date = persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);

                var calendarDate = await _shiftDateRepository.GetByFilterAsync(
                    new ShiftDateFilter(x => x.Date == date.Date)
                );

                if (calendarDate.Items.Count == 0)
                {
                    throw new Exception("تاریخ مورد نظر یافت نشد");
                }

                var shiftDate = calendarDate.Items.First();
                shiftDate.IsHoliday = true;
                shiftDate.HolidayEvent = holidayEvent;
                shiftDate.UpdateDate = DateTime.Now;

                _shiftDateRepository.Update(shiftDate);
                await _shiftDateRepository.SaveAsync();
            }
            catch (FormatException)
            {
                throw new Exception("فرمت تاریخ شمسی نامعتبر است. فرمت صحیح: yyyy/MM/dd");
            }
            catch (Exception ex) when (ex.Message != "تاریخ مورد نظر یافت نشد")
            {
                throw new Exception("خطا در تبدیل تاریخ: " + ex.Message);
            }
        }

        public async Task SetAsRegularDayAsync(string persianDate, string? holidayEvent)
        {
            try
            {
                // تبدیل تاریخ شمسی به میلادی
                var dateParts = persianDate.Split('/');
                if (dateParts.Length != 3)
                    throw new Exception("فرمت تاریخ شمسی نامعتبر است. فرمت صحیح: yyyy/MM/dd");

                var persianCalendar = new PersianCalendar();
                var year = int.Parse(dateParts[0]);
                var month = int.Parse(dateParts[1]);
                var day = int.Parse(dateParts[2]);

                var date = persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);

                var calendarDate = await _shiftDateRepository.GetByFilterAsync(
                    new ShiftDateFilter(x => x.Date == date.Date)
                );

                if (calendarDate.Items.Count == 0)
                {
                    throw new Exception("تاریخ مورد نظر یافت نشد");
                }

                var shiftDate = calendarDate.Items.First();
                shiftDate.IsHoliday = false;
                shiftDate.HolidayEvent = holidayEvent;
                shiftDate.UpdateDate = DateTime.Now;

                _shiftDateRepository.Update(shiftDate);
                await _shiftDateRepository.SaveAsync();
            }
            catch (FormatException)
            {
                throw new Exception("فرمت تاریخ شمسی نامعتبر است. فرمت صحیح: yyyy/MM/dd");
            }
            catch (Exception ex) when (ex.Message != "تاریخ مورد نظر یافت نشد")
            {
                throw new Exception("خطا در تبدیل تاریخ: " + ex.Message);
            }
        }

        // گرفتن نام فارسی روز هفته
        private string GetPersianDayOfWeekTitle(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Saturday => "شنبه",
                DayOfWeek.Sunday => "یکشنبه",
                DayOfWeek.Monday => "دوشنبه",
                DayOfWeek.Tuesday => "سه‌شنبه",
                DayOfWeek.Wednesday => "چهارشنبه",
                DayOfWeek.Thursday => "پنجشنبه",
                DayOfWeek.Friday => "جمعه",
                _ => "نامشخص"
            };
        }
    }

    // کلاس مدل تعطیلات
    public class HolidayEntry
    {
        public string Date { get; set; } = default!; // yyyy/MM/dd
        public string Title { get; set; } = default!;
    }
}
