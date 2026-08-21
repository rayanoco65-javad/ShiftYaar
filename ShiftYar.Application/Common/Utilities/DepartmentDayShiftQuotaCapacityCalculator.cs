using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

public static class DepartmentDayShiftQuotaCapacityCalculator
{
    public static (int TotalShiftSlots, int HolidayShiftSlots, int HeadcountPerRegularDay, int HeadcountPerHolidayDay)
        Calculate(
            ShiftLabel shiftLabel,
            IEnumerable<Shift> departmentShifts,
            IReadOnlyCollection<DateTime> monthDates,
            ISet<DateTime> holidayDates)
    {
        var labelShifts = (departmentShifts ?? Enumerable.Empty<Shift>())
            .Where(s => s.Label == shiftLabel)
            .ToList();

        var headcountPerRegularDay = SumRequiredHeadcount(labelShifts, isHoliday: false);
        var headcountPerHolidayDay = SumRequiredHeadcount(labelShifts, isHoliday: true);

        if (headcountPerRegularDay <= 0 && headcountPerHolidayDay <= 0)
        {
            headcountPerRegularDay = 1;
            headcountPerHolidayDay = 1;
        }
        else if (headcountPerHolidayDay <= 0)
        {
            headcountPerHolidayDay = headcountPerRegularDay;
        }

        var distinctDays = monthDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var totalShiftSlots = 0;
        var holidayShiftSlots = 0;

        foreach (var date in distinctDays)
        {
            var isHoliday = holidayDates.Contains(date);
            var dailyCapacity = isHoliday ? headcountPerHolidayDay : headcountPerRegularDay;
            totalShiftSlots += dailyCapacity;
            if (isHoliday)
            {
                holidayShiftSlots += dailyCapacity;
            }
        }

        return (totalShiftSlots, holidayShiftSlots, headcountPerRegularDay, headcountPerHolidayDay);
    }

    private static int SumRequiredHeadcount(IReadOnlyCollection<Shift> labelShifts, bool isHoliday)
    {
        var sum = 0;
        foreach (var shift in labelShifts)
        {
            foreach (var requirement in shift.RequiredSpecialties ?? Enumerable.Empty<ShiftRequiredSpecialty>())
            {
                sum += ApprovedOnShiftCapacityValidator.GetEffectiveRequiredTotalCount(requirement, isHoliday);
            }
        }

        return sum;
    }
}
