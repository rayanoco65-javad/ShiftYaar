using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Features.CalendarSeeder.Filters;
using ShiftYar.Domain.Entities.ShiftDateModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.CalendarSeeder
{
    public interface ICalendarSeederService
    {
        Task SeedShiftDatesAsync(int year);
        Task<ApiResponse<PagedResponse<ShiftDate>>> GetDatesAsync(ShiftDateFilter filter);
        Task SetAsHolidayAsync(string persianDate, string holidayEvent);
        Task SetAsRegularDayAsync(string persianDate, string holidayEvent);
    }
}
