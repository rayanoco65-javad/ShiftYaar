using ShiftYar.Application.Features.UserModel.Services;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// ظرفیت مجموع سهمیه شب ماهانه دپارتمان = تعداد شب‌های ماه × نفر موردنیاز در هر شیفت شب
/// (جمع <see cref="ShiftRequiredSpecialty.RequiredTottalCount"/> برای شیفت‌های شب).
/// </summary>
public static class DepartmentNightQuotaCapacityCalculator
{
    public static (int TotalNightSlots, int HolidayWeekendNightSlots, int HeadcountPerRegularNight, int HeadcountPerHolidayNight)
        Calculate(
            IEnumerable<Shift> departmentShifts,
            IReadOnlyCollection<DateTime> monthDates,
            ISet<DateTime> holidayDates)
    {
        var nightShifts = (departmentShifts ?? Enumerable.Empty<Shift>())
            .Where(s => s.Label == ShiftLabel.Night)
            .ToList();

        var headcountPerRegularNight = SumRequiredHeadcount(nightShifts, isHoliday: false);
        var headcountPerHolidayNight = SumRequiredHeadcount(nightShifts, isHoliday: true);

        if (headcountPerRegularNight <= 0 && headcountPerHolidayNight <= 0)
        {
            headcountPerRegularNight = 1;
            headcountPerHolidayNight = 1;
        }
        else if (headcountPerHolidayNight <= 0)
        {
            headcountPerHolidayNight = headcountPerRegularNight;
        }

        var distinctDays = monthDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var totalNightSlots = 0;
        var holidayWeekendNightSlots = 0;

        foreach (var date in distinctDays)
        {
            var isHoliday = holidayDates.Contains(date);
            var dailyCapacity = isHoliday ? headcountPerHolidayNight : headcountPerRegularNight;
            totalNightSlots += dailyCapacity;

            if (HolidayWeekendNightRules.IsHolidayWeekendNight(date, holidayDates))
            {
                holidayWeekendNightSlots += dailyCapacity;
            }
        }

        return (totalNightSlots, holidayWeekendNightSlots, headcountPerRegularNight, headcountPerHolidayNight);
    }

    private static int SumRequiredHeadcount(IReadOnlyCollection<Shift> nightShifts, bool isHoliday)
    {
        var sum = 0;
        foreach (var shift in nightShifts)
        {
            foreach (var requirement in shift.RequiredSpecialties ?? Enumerable.Empty<ShiftRequiredSpecialty>())
            {
                sum += ApprovedOnShiftCapacityValidator.GetEffectiveRequiredTotalCount(requirement, isHoliday);
            }
        }

        return sum;
    }
}
