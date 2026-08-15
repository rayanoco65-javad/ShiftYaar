using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// بررسی اینکه تأیید درخواست حضور (ON) از ظرفیت شیفت/تخصص در یک روز عبور نکند.
/// </summary>
public static class ApprovedOnShiftCapacityValidator
{
    public static int GetEffectiveRequiredTotalCount(ShiftRequiredSpecialty specialty, bool isHoliday)
    {
        if (isHoliday)
        {
            return specialty.HolidayRequiredTottalCount ?? specialty.RequiredTottalCount ?? 0;
        }

        return specialty.RequiredTottalCount ?? 0;
    }

    public static int ResolveCapacityForSpecialty(
        IEnumerable<Shift> shifts,
        ShiftLabel shiftLabel,
        int specialtyId,
        bool isHoliday)
    {
        int? capacity = null;
        foreach (var shift in shifts.Where(s => s.Label == shiftLabel))
        {
            var requirement = shift.RequiredSpecialties?
                .FirstOrDefault(rs => rs.SpecialtyId == specialtyId);
            if (requirement == null)
            {
                continue;
            }

            var dayCapacity = GetEffectiveRequiredTotalCount(requirement, isHoliday);
            capacity = capacity.HasValue ? Math.Max(capacity.Value, dayCapacity) : dayCapacity;
        }

        return capacity ?? 0;
    }

    public static bool WouldExceedCapacity(int approvedOnCount, int capacity) =>
        capacity > 0 && approvedOnCount + 1 > capacity;

    public static string? BuildExceededCapacityMessage(
        int approvedOnCount,
        int capacity,
        ShiftLabel shiftLabel,
        DateTime requestDate,
        IEnumerable<string>? approvedUserDisplayNames = null)
    {
        if (!WouldExceedCapacity(approvedOnCount, capacity))
        {
            return null;
        }

        var shiftName = GetShiftLabelDisplayName(shiftLabel);
        var persianDate = DateConverter.ConvertToPersianDate(requestDate);
        var names = approvedUserDisplayNames?
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();
        var namesPart = names is { Count: > 0 }
            ? $" ({string.Join("، ", names)})"
            : string.Empty;

        return
            $"ظرفیت شیفت {shiftName} در تاریخ {persianDate} تکمیل شده است. " +
            $"در حال حاضر {approvedOnCount} درخواست حضور تأییدشده{namesPart} وجود دارد و ظرفیت این شیفت {capacity} نفر است. " +
            "امکان تأیید درخواست حضور بیش از ظرفیت وجود ندارد.";
    }

    public static string GetShiftLabelDisplayName(ShiftLabel shiftLabel) =>
        shiftLabel switch
        {
            ShiftLabel.Morning => "صبح",
            ShiftLabel.Evening => "عصر",
            ShiftLabel.Night => "شب",
            _ => shiftLabel.ToString()
        };
}
