using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// اعتبارسنجی سقف مجاز مرخصی روزانه (حداکثر مرخصی مجاز در روز)
/// فرمول:
/// حداقل پرسنل مورد نیاز امروز = مجموع نیاز شیفت‌های امروز + نیاز شیفت شب روز قبل (استراحت اجباری)
/// حداکثر مرخصی مجاز روزانه = کل پرسنل فعال - حداقل پرسنل مورد نیاز امروز
/// </summary>
public static class ApprovedLeaveCapacityValidator
{
    public static int GetEffectiveRequiredTotalCount(ShiftRequiredSpecialty specialty, bool isHoliday)
    {
        if (isHoliday)
        {
            return specialty.HolidayRequiredTottalCount ?? specialty.RequiredTottalCount ?? 0;
        }

        return specialty.RequiredTottalCount ?? 0;
    }

    /// <summary>
    /// مجموع نیاز تمام شیفت‌های دپارتمان برای یک تخصص در یک روز مشخص
    /// </summary>
    public static int CalculateDailyShiftDemandForSpecialty(
        IEnumerable<Shift> shifts,
        int specialtyId,
        bool isHoliday)
    {
        var total = 0;
        foreach (var shift in shifts)
        {
            var req = shift.RequiredSpecialties?.FirstOrDefault(rs => rs.SpecialtyId == specialtyId);
            if (req != null)
            {
                total += GetEffectiveRequiredTotalCount(req, isHoliday);
            }
        }
        return total;
    }

    /// <summary>
    /// نیاز شیفت شب برای یک تخصص در روز قبل (که به دلیل استراحت اجباری، امروز نمی‌توانند کار کنند)
    /// </summary>
    public static int CalculateNightShiftDemandForSpecialty(
        IEnumerable<Shift> shifts,
        int specialtyId,
        bool isHoliday)
    {
        var total = 0;
        foreach (var shift in shifts)
        {
            var label = shift.Label ?? ShiftLabelResolver.InferLabelFromStartTime(shift);
            if (label == ShiftLabel.Night)
            {
                var req = shift.RequiredSpecialties?.FirstOrDefault(rs => rs.SpecialtyId == specialtyId);
                if (req != null)
                {
                    total += GetEffectiveRequiredTotalCount(req, isHoliday);
                }
            }
        }
        return total;
    }

    /// <summary>
    /// محاسبه سقف مجاز مرخصی روزانه:
    /// MaxDailyLeaveCapacity = max(0, TotalActivePersonnel - (TodayDemand + YesterdayNightDemand))
    /// </summary>
    public static int CalculateMaxDailyLeaveCapacity(
        int totalActivePersonnel,
        int todayShiftDemand,
        int yesterdayNightShiftDemand)
    {
        var minPersonnelRequired = todayShiftDemand + yesterdayNightShiftDemand;
        return Math.Max(0, totalActivePersonnel - minPersonnelRequired);
    }

    /// <summary>
    /// آیا افزودن یک مرخصی جدید، از سقف مجاز مرخصی روزانه فراتر می‌رود؟
    /// </summary>
    public static bool WouldExceedCapacity(int approvedLeaveCount, int maxCapacity) =>
        approvedLeaveCount + 1 > maxCapacity;

    /// <summary>
    /// ساخت پیام خطای مناسب و شفاف فارسی برای نمایش به سوپروایزر
    /// </summary>
    public static string? BuildExceededCapacityMessage(
        int approvedLeaveCount,
        int maxCapacity,
        int totalActivePersonnel,
        int todayShiftDemand,
        int yesterdayNightDemand,
        DateTime requestDate,
        string? specialtyName = null,
        IEnumerable<string>? approvedUserDisplayNames = null)
    {
        if (!WouldExceedCapacity(approvedLeaveCount, maxCapacity))
        {
            return null;
        }

        var persianDate = DateConverter.ConvertToPersianDate(requestDate);
        var specialtyPart = !string.IsNullOrWhiteSpace(specialtyName)
            ? $" برای تخصص «{specialtyName.Trim()}»"
            : string.Empty;

        var names = approvedUserDisplayNames?
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();
        var namesPart = names is { Count: > 0 }
            ? $" ({string.Join("، ", names)})"
            : string.Empty;

        var minRequired = todayShiftDemand + yesterdayNightDemand;

        if (maxCapacity == 0)
        {
            return
                $"به دلیل محدودیت شدید نیرو در تاریخ {persianDate}{specialtyPart}، امکان اعطای مرخصی وجود ندارد. " +
                $"حداقل {minRequired} نفر نیرو برای پوشش شیفت‌های امروز ({todayShiftDemand} نفر) و استراحت اجباری شیفت شب روز قبل ({yesterdayNightDemand} نفر) الزامی است، " +
                $"در حالی که کل پرسنل فعال این بخش {totalActivePersonnel} نفر است.";
        }

        return
            $"ظرفیت مرخصی روزانه در تاریخ {persianDate}{specialtyPart} تکمیل شده است. " +
            $"حداکثر سقف مجاز مرخصی همزمان در این تاریخ {maxCapacity} نفر است " +
            $"(کل پرسنل فعال: {totalActivePersonnel} نفر، مجموع نیاز شیفت‌های امروز: {todayShiftDemand} نفر، استراحت اجباری شب روز قبل: {yesterdayNightDemand} نفر). " +
            $"در حال حاضر {approvedLeaveCount} مرخصی تأییدشده{namesPart} وجود دارد. " +
            "امکان تأیید مرخصی جدید در این تاریخ وجود ندارد زیرا منجر به کمبود فیزیکی نیرو و عدم امکان شیفت‌بندی خواهد شد.";
    }
}
