using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// اعتبارسنجی سقف مجاز مرخصی روزانه (حداکثر مرخصی مجاز در روز)
/// فرمول:
/// حداقل پرسنل مورد نیاز امروز = (مجموع نیاز شیفت‌های امروز - حداکثر شیفت‌های لانگ قابل تشکیل) + نیاز شیفت شب روز قبل (استراحت اجباری)
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
    /// بررسی صلاحیت پرسنل برای شیفت لانگ صبح+عصر.
    /// شیفت‌های لانگ در بخش «مجوزهای نوع شیفت» تنها در صورتی فعال می‌شوند که «حداکثر شیفت در روز» دپارتمان روی ۲ تنظیم شده باشد.
    /// این تنظیم توسط «مجوزهای نوع شیفت» کاربر کاستوم می‌شود.
    /// </summary>
    public static bool CanUserPerformMorningEvening(User? user, int maxShiftsPerDay = 1)
    {
        if (user == null || maxShiftsPerDay < 2) return false;
        if (user.ShiftType == ShiftTypes.FixedShift) return false;
        if (user.MorningRequiresEvening == true || user.EveningRequiresMorning == true)
        {
            return true;
        }
        if (user.AllowedShiftPermissions.HasValue)
        {
            return user.AllowedShiftPermissions.Value.HasFlag(UserShiftPermission.MorningEveningSameDay);
        }
        if (user.ShiftSubType == ShiftSubTypes.TwoShifts && user.TwoShiftRotationPattern == TwoShiftRotationPattern.MorningEvening)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// بررسی صلاحیت پرسنل برای شیفت لانگ صبح+شب.
    /// شیفت‌های لانگ در بخش «مجوزهای نوع شیفت» تنها در صورتی فعال می‌شوند که «حداکثر شیفت در روز» دپارتمان روی ۲ تنظیم شده باشد.
    /// این تنظیم توسط «مجوزهای نوع شیفت» کاربر کاستوم می‌شود.
    /// </summary>
    public static bool CanUserPerformMorningNight(User? user, int maxShiftsPerDay = 1)
    {
        if (user == null || maxShiftsPerDay < 2) return false;
        if (user.ShiftType == ShiftTypes.FixedShift) return false;
        if (user.MorningRequiresNight == true || user.NightRequiresMorning == true)
        {
            return true;
        }
        if (user.AllowedShiftPermissions.HasValue)
        {
            return user.AllowedShiftPermissions.Value.HasFlag(UserShiftPermission.MorningNightSameDay);
        }
        if (user.ShiftSubType == ShiftSubTypes.TwoShifts && user.TwoShiftRotationPattern == TwoShiftRotationPattern.MorningNight)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// آیا کاربر مجاز به شیفت لانگ (صبح+عصر یا صبح+شب) است؟
    /// شیفت‌های لانگ تنها در صورتی فعال می‌شوند که «حداکثر شیفت در روز» روی ۲ تنظیم شده باشد.
    /// </summary>
    public static bool CanUserPerformLongShift(User? user, int maxShiftsPerDay = 1) =>
        CanUserPerformMorningEvening(user, maxShiftsPerDay) || CanUserPerformMorningNight(user, maxShiftsPerDay);

    /// <summary>
    /// تفکیک نیاز شیفت‌های امروز برای یک تخصص بر اساس نوبت (صبح، عصر، شب و سایر)
    /// </summary>
    public static (int morningDemand, int eveningDemand, int nightDemand, int otherDemand) CalculateShiftDemandsByLabelForSpecialty(
        IEnumerable<Shift> shifts,
        int specialtyId,
        bool isHoliday)
    {
        int morning = 0, evening = 0, night = 0, other = 0;
        foreach (var shift in shifts)
        {
            var reqs = shift.RequiredSpecialties?
                .Where(rs => specialtyId == 0 ? (rs.SpecialtyId == null || rs.SpecialtyId == 0) : rs.SpecialtyId == specialtyId);
            if (reqs == null) continue;

            var shiftReqTotal = 0;
            foreach (var req in reqs)
            {
                shiftReqTotal += GetEffectiveRequiredTotalCount(req, isHoliday);
            }

            var label = shift.Label ?? ShiftLabelResolver.InferLabelFromStartTime(shift);
            switch (label)
            {
                case ShiftLabel.Morning:
                    morning += shiftReqTotal;
                    break;
                case ShiftLabel.Evening:
                    evening += shiftReqTotal;
                    break;
                case ShiftLabel.Night:
                    night += shiftReqTotal;
                    break;
                default:
                    other += shiftReqTotal;
                    break;
            }
        }
        return (morning, evening, night, other);
    }

    /// <summary>
    /// بررسی مجاز بودن کاربر برای یک برچسب شیفت با در نظر گرفتن نوع شیفت و مجوزها
    /// </summary>
    public static bool MayUserTakeLabel(User? user, ShiftLabel label)
    {
        if (user == null) return false;
        var perms = ShiftEligibilityResolver.ResolvePermissions(
            user.AllowedShiftPermissions,
            user.ShiftType ?? ShiftTypes.RotatingShift,
            user.ShiftSubType ?? ShiftSubTypes.ThreeShifts,
            user.TwoShiftRotationPattern);

        return label switch
        {
            ShiftLabel.Morning => perms.HasFlag(UserShiftPermission.Morning),
            ShiftLabel.Evening => perms.HasFlag(UserShiftPermission.Evening),
            ShiftLabel.Night => perms.HasFlag(UserShiftPermission.Night),
            _ => true
        };
    }

    /// <summary>
    /// محاسبه حداکثر تعداد شیفت‌های لانگ (صبح+عصر یا صبح+شب) که با ترکیب پرسنل واجد شرایط و شیفت‌های امروز قابل پوشش است.
    /// هر شیفت لانگ به منزله پوشش ۲ شیفت توسط ۱ نفر است و ۱ نفر از حداقل نیروی فیزیکی مورد نیاز کسر می‌کند.
    /// شیفت‌های لانگ در بخش «مجوزهای نوع شیفت» تنها در صورتی فعال می‌شوند که «حداکثر شیفت در روز» دپارتمان روی ۲ تنظیم شده باشد.
    /// </summary>
    public static int CalculateMaxLongShifts(
        int morningDemand,
        int eveningDemand,
        int nightDemand,
        IEnumerable<User>? users,
        int maxShiftsPerDay = 1,
        IEnumerable<ShiftRequest>? approvedRequestsOnDate = null,
        IEnumerable<ShiftRequest>? approvedRequestsYesterday = null,
        ShiftRequest? pendingRequest = null)
    {
        if (maxShiftsPerDay < 2 || users == null || morningDemand <= 0 || (eveningDemand <= 0 && nightDemand <= 0))
        {
            return 0;
        }

        var allRequestsToday = (approvedRequestsOnDate ?? Enumerable.Empty<ShiftRequest>()).ToList();
        if (pendingRequest != null)
        {
            allRequestsToday.Add(pendingRequest);
        }

        var allRequestsYesterday = (approvedRequestsYesterday ?? Enumerable.Empty<ShiftRequest>()).ToList();

        var fullDayOffUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOffShift &&
                        r.RequestType == RequestType.FullDay)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var morningOffUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOffShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Morning)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var eveningOffUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOffShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Evening)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var nightOffUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOffShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Night)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var todayNightOnUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOnShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Night)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var yesterdayNightOnUserIds = allRequestsYesterday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOnShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Night)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        int countMeOnly = 0;
        int countMnOnly = 0;
        int countBoth = 0;

        foreach (var user in users)
        {
            if (!user.Id.HasValue) continue;
            var uid = user.Id.Value;

            if (fullDayOffUserIds.Contains(uid)) continue;
            if (yesterdayNightOnUserIds.Contains(uid)) continue;

            var canDoMorning = !morningOffUserIds.Contains(uid) &&
                               !todayNightOnUserIds.Contains(uid) &&
                               MayUserTakeLabel(user, ShiftLabel.Morning);

            var canDoEvening = !eveningOffUserIds.Contains(uid) &&
                               !todayNightOnUserIds.Contains(uid) &&
                               MayUserTakeLabel(user, ShiftLabel.Evening);

            var canDoNight = !nightOffUserIds.Contains(uid) &&
                             MayUserTakeLabel(user, ShiftLabel.Night);

            var canMe = canDoMorning && canDoEvening && CanUserPerformMorningEvening(user, maxShiftsPerDay);
            var canMn = canDoMorning && canDoNight && CanUserPerformMorningNight(user, maxShiftsPerDay);

            if (canMe && canMn)
            {
                countBoth++;
            }
            else if (canMe)
            {
                countMeOnly++;
            }
            else if (canMn)
            {
                countMnOnly++;
            }
        }

        if (countMeOnly == 0 && countMnOnly == 0 && countBoth == 0)
        {
            return 0;
        }

        // ۱. پرسنلی که فقط مجاز به صبح+عصر هستند
        var usedMe = Math.Min(countMeOnly, eveningDemand);
        var remEvening = eveningDemand - usedMe;

        // ۲. پرسنلی که فقط مجاز به صبح+شب هستند
        var usedMn = Math.Min(countMnOnly, nightDemand);
        var remNight = nightDemand - usedMn;

        // ۳. پرسنلی که هر دو ترکیب را مجاز هستند می‌توانند باقیمانده عصر یا شب را پر کنند
        var usedBoth = Math.Min(countBoth, remEvening + remNight);

        var totalNonMorningCovered = usedMe + usedMn + usedBoth;

        // ۴. هر شیفت لانگ دقیقاً نیازمند ۱ شیفت صبح است
        return Math.Min(morningDemand, totalNonMorningCovered);
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
            var reqs = shift.RequiredSpecialties?
                .Where(rs => specialtyId == 0 ? (rs.SpecialtyId == null || rs.SpecialtyId == 0) : rs.SpecialtyId == specialtyId);
            if (reqs != null)
            {
                foreach (var req in reqs)
                {
                    total += GetEffectiveRequiredTotalCount(req, isHoliday);
                }
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
                var reqs = shift.RequiredSpecialties?
                    .Where(rs => specialtyId == 0 ? (rs.SpecialtyId == null || rs.SpecialtyId == 0) : rs.SpecialtyId == specialtyId);
                if (reqs != null)
                {
                    foreach (var req in reqs)
                    {
                        total += GetEffectiveRequiredTotalCount(req, isHoliday);
                    }
                }
            }
        }
        return total;
    }

    /// <summary>
    /// محاسبه سقف مجاز مرخصی روزانه با در نظر گرفتن امکان شیفت لانگ:
    /// حداقل پرسنل مورد نیاز امروز = (مجموع نیاز شیفت‌های امروز - حداکثر شیفت‌های لانگ) + استراحت شب قبل
    /// MaxDailyLeaveCapacity = max(0, TotalActivePersonnel - minPersonnelRequired)
    /// </summary>
    public static int CalculateMaxDailyLeaveCapacity(
        int totalActivePersonnel,
        int todayShiftDemand,
        int yesterdayNightShiftDemand,
        int maxLongShifts = 0)
    {
        var effectiveTodayDemand = Math.Max(0, todayShiftDemand - maxLongShifts);
        var minPersonnelRequired = effectiveTodayDemand + yesterdayNightShiftDemand;
        return Math.Max(0, totalActivePersonnel - minPersonnelRequired);
    }

    /// <summary>
    /// آیا افزودن یک مرخصی جدید، از سقف مجاز مرخصی روزانه فراتر می‌رود؟
    /// </summary>
    public static bool WouldExceedCapacity(int approvedLeaveCount, int maxCapacity) =>
        approvedLeaveCount + 1 > maxCapacity;

    /// <summary>
    /// اعتبارسنجی توان پوشش فیزیکی شیفت‌های روز توسط پرسنل حاضر باقیمانده (با احتساب شیفت لانگ).
    /// در صورتی که با اعطای مرخصی، نیروهای حاضر حتی با حداکثر شیفت لانگ قادر به پوشش شیفت‌ها نباشند خطا برمی‌گرداند.
    /// </summary>
    public static string? ValidateAvailablePersonnelCoverage(
        int availablePersonnelCount,
        int availableLongShifts,
        int todayShiftDemand,
        DateTime requestDate,
        string? contextTitle = null,
        int maxShiftsPerDay = 1,
        int yesterdayNightDemand = 0)
    {
        var maxCoverableShifts = availablePersonnelCount + availableLongShifts;
        var totalRequired = todayShiftDemand + yesterdayNightDemand;
        if (maxCoverableShifts < totalRequired)
        {
            var persianDate = DateConverter.ConvertToPersianDate(requestDate);
            var titlePart = !string.IsNullOrWhiteSpace(contextTitle)
                ? $" برای «{contextTitle.Trim()}»"
                : string.Empty;

            var longShiftNote = availableLongShifts > 0
                ? $" (شامل {availablePersonnelCount} نفر حاضر و {availableLongShifts} شیفت لانگ قابل پوشش)"
                : (maxShiftsPerDay < 2
                    ? " (تنظیم «حداکثر شیفت در روز» دپارتمان روی ۱ است و امکان پوشش لانگ وجود ندارد)"
                    : " (هیچ‌یک از پرسنل حاضر دارای مجوز شیفت لانگ فعال نیستند)");

            var nightRestNote = yesterdayNightDemand > 0
                ? $" و {yesterdayNightDemand} نفر استراحت اجباری شیفت شب روز قبل (مجموعاً {totalRequired} سهمیه نیروی لازم)"
                : string.Empty;

            return
                $"امکان تأیید مرخصی در تاریخ {persianDate}{titlePart} وجود ندارد. " +
                $"پرسنل حاضر حداکثر توانایی پوشش {maxCoverableShifts} شیفت را دارند{longShiftNote}، " +
                $"در حالی که نیاز شیفت‌های این روز {todayShiftDemand} شیفت{nightRestNote} است و تأیید این مرخصی منجر به کمبود نیرو خواهد شد.";
        }

        return null;
    }

    /// <summary>
    /// ساخت پیام خطای مناسب و شفاف فارسی برای نمایش به سوپروایزر با شفاف‌سازی سهم شیفت‌های لانگ
    /// </summary>
    public static string? BuildExceededCapacityMessage(
        int approvedLeaveCount,
        int maxCapacity,
        int totalActivePersonnel,
        int todayShiftDemand,
        int yesterdayNightDemand,
        DateTime requestDate,
        string? specialtyName = null,
        IEnumerable<string>? approvedUserDisplayNames = null,
        int maxLongShifts = 0)
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

        var effectiveTodayDemand = Math.Max(0, todayShiftDemand - maxLongShifts);
        var minRequired = effectiveTodayDemand + yesterdayNightDemand;
        var longShiftPart = maxLongShifts > 0
            ? $" با احتساب {maxLongShifts} شیفت لانگ از مجموع {todayShiftDemand} شیفت"
            : string.Empty;

        if (maxCapacity == 0)
        {
            return
                $"به دلیل محدودیت شدید نیرو در تاریخ {persianDate}{specialtyPart}، امکان اعطای مرخصی وجود ندارد. " +
                $"حداقل {minRequired} نفر نیرو برای پوشش شیفت‌های امروز ({effectiveTodayDemand} نفر{longShiftPart}) و استراحت اجباری شیفت شب روز قبل ({yesterdayNightDemand} نفر) الزامی است، " +
                $"در حالی که کل پرسنل فعال این بخش {totalActivePersonnel} نفر است.";
        }

        return
            $"ظرفیت مرخصی روزانه در تاریخ {persianDate}{specialtyPart} تکمیل شده است. " +
            $"حداکثر سقف مجاز مرخصی همزمان در این تاریخ {maxCapacity} نفر است " +
            $"(کل پرسنل فعال: {totalActivePersonnel} نفر، مجموع نیاز شیفت‌های امروز: {todayShiftDemand} نفر{longShiftPart}، استراحت اجباری شب روز قبل: {yesterdayNightDemand} نفر). " +
            $"در حال حاضر {approvedLeaveCount} مرخصی تأییدشده{namesPart} وجود دارد. " +
            "امکان تأیید مرخصی جدید در این تاریخ وجود ندارد زیرا منجر به کمبود فیزیکی نیرو و عدم امکان شیفت‌بندی خواهد شد.";
    }

    /// <summary>
    /// اعتبارسنجی جامع توان پوشش شیفت‌های روز بر اساس نوبت (صبح، عصر، شب و مجموع کل)
    /// با احتساب استراحت شب قبل، مرخصی‌ها، عدم حضور در شیفت خاص و شیفت‌های لانگ مجاز.
    /// </summary>
    public static string? ValidateDailyShiftCoverage(
        int morningDemand,
        int eveningDemand,
        int nightDemand,
        int yesterdayNightDemand,
        IEnumerable<User> departmentUsers,
        DateTime requestDate,
        int maxShiftsPerDay = 1,
        IEnumerable<ShiftRequest>? approvedRequestsOnDate = null,
        IEnumerable<ShiftRequest>? approvedRequestsYesterday = null,
        ShiftRequest? pendingRequest = null)
    {
        var usersList = departmentUsers.ToList();
        var allRequestsToday = (approvedRequestsOnDate ?? Enumerable.Empty<ShiftRequest>()).ToList();
        if (pendingRequest != null)
        {
            allRequestsToday.Add(pendingRequest);
        }

        var allRequestsYesterday = (approvedRequestsYesterday ?? Enumerable.Empty<ShiftRequest>()).ToList();

        var fullDayOffUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOffShift &&
                        r.RequestType == RequestType.FullDay)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var morningOffUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOffShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Morning)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var eveningOffUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOffShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Evening)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var nightOffUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOffShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Night)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var todayNightOnUserIds = allRequestsToday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOnShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Night)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var yesterdayNightOnUserIds = allRequestsYesterday
            .Where(r => r.UserId.HasValue &&
                        r.RequestAction == RequestAction.RequestToBeOnShift &&
                        r.RequestType == RequestType.SpecificShift &&
                        r.ShiftLabel == ShiftLabel.Night)
            .Select(r => r.UserId!.Value)
            .ToHashSet();

        var persianDate = DateConverter.ConvertToPersianDate(requestDate);

        // ۱. بررسی امکان پوشش تک‌تک شیفت‌ها
        var availableForMorning = usersList
            .Where(u => u.Id.HasValue &&
                        !fullDayOffUserIds.Contains(u.Id.Value) &&
                        !yesterdayNightOnUserIds.Contains(u.Id.Value) &&
                        !morningOffUserIds.Contains(u.Id.Value) &&
                        !todayNightOnUserIds.Contains(u.Id.Value) &&
                        MayUserTakeLabel(u, ShiftLabel.Morning))
            .ToList();

        if (availableForMorning.Count < morningDemand)
        {
            return $"امکان تأیید این درخواست وجود ندارد؛ حداقل {morningDemand} نفر برای پوشش شیفت صبح در تاریخ {persianDate} الزامی است، " +
                   $"در حالی که با تأیید این درخواست تنها {availableForMorning.Count} نفر نیروی واجد شرایط در دسترس خواهند بود و منجر به کسری نیرو در شیفت‌بندی می‌شود.";
        }

        var availableForEvening = usersList
            .Where(u => u.Id.HasValue &&
                        !fullDayOffUserIds.Contains(u.Id.Value) &&
                        !yesterdayNightOnUserIds.Contains(u.Id.Value) &&
                        !eveningOffUserIds.Contains(u.Id.Value) &&
                        !todayNightOnUserIds.Contains(u.Id.Value) &&
                        MayUserTakeLabel(u, ShiftLabel.Evening))
            .ToList();

        if (availableForEvening.Count < eveningDemand)
        {
            return $"امکان تأیید این درخواست وجود ندارد؛ حداقل {eveningDemand} نفر برای پوشش شیفت عصر در تاریخ {persianDate} الزامی است، " +
                   $"در حالی که با تأیید این درخواست تنها {availableForEvening.Count} نفر نیروی واجد شرایط در دسترس خواهند بود و منجر به کسری نیرو در شیفت‌بندی می‌شود.";
        }

        var availableForNight = usersList
            .Where(u => u.Id.HasValue &&
                        !fullDayOffUserIds.Contains(u.Id.Value) &&
                        !nightOffUserIds.Contains(u.Id.Value) &&
                        MayUserTakeLabel(u, ShiftLabel.Night))
            .ToList();

        if (availableForNight.Count < nightDemand)
        {
            return $"امکان تأیید این درخواست وجود ندارد؛ حداقل {nightDemand} نفر برای پوشش شیفت شب در تاریخ {persianDate} الزامی است، " +
                   $"در حالی که با تأیید این درخواست تنها {availableForNight.Count} نفر نیروی واجد شرایط در دسترس خواهند بود و منجر به کسری نیرو در شیفت‌بندی می‌شود.";
        }

        // ۲. بررسی ظرفیت توأم صبح و عصر (با احتساب شیفت‌های لانگ صبح+عصر)
        if (morningDemand > 0 && eveningDemand > 0)
        {
            var availableForMorningOrEvening = usersList
                .Where(u => u.Id.HasValue &&
                            !fullDayOffUserIds.Contains(u.Id.Value) &&
                            !yesterdayNightOnUserIds.Contains(u.Id.Value) &&
                            !todayNightOnUserIds.Contains(u.Id.Value) &&
                            ((!morningOffUserIds.Contains(u.Id.Value) && MayUserTakeLabel(u, ShiftLabel.Morning)) ||
                             (!eveningOffUserIds.Contains(u.Id.Value) && MayUserTakeLabel(u, ShiftLabel.Evening))))
                .ToList();

            var longShiftMECandidates = availableForMorningOrEvening
                .Count(u => !morningOffUserIds.Contains(u.Id!.Value) &&
                            !eveningOffUserIds.Contains(u.Id!.Value) &&
                            MayUserTakeLabel(u, ShiftLabel.Morning) &&
                            MayUserTakeLabel(u, ShiftLabel.Evening) &&
                            CanUserPerformMorningEvening(u, maxShiftsPerDay));

            var maxLongME = Math.Min(longShiftMECandidates, Math.Min(morningDemand, eveningDemand));
            var totalCoverableME = availableForMorningOrEvening.Count + maxLongME;
            var totalRequiredME = morningDemand + eveningDemand;

            if (totalCoverableME < totalRequiredME)
            {
                return $"امکان تأیید این درخواست وجود ندارد؛ مجموع ظرفیت پوشش شیفت‌های صبح و عصر در تاریخ {persianDate} " +
                       $"({totalCoverableME} شیفت با احتساب {maxLongME} شیفت لانگ مجاز) کمتر از مجموع نیاز این دو شیفت " +
                       $"({totalRequiredME} شیفت: {morningDemand} صبح + {eveningDemand} عصر) است و منجر به کسری نیرو در شیفت‌بندی می‌شود.";
            }
        }

        // ۳. بررسی ظرفیت کلی شیفت‌های روز
        var availablePersonnel = usersList
            .Where(u => u.Id.HasValue && !fullDayOffUserIds.Contains(u.Id.Value))
            .ToList();

        var availableLongShifts = CalculateMaxLongShifts(
            morningDemand,
            eveningDemand,
            nightDemand,
            availablePersonnel,
            maxShiftsPerDay,
            approvedRequestsOnDate,
            approvedRequestsYesterday,
            pendingRequest);

        var totalShiftDemand = morningDemand + eveningDemand + nightDemand;
        return ValidateAvailablePersonnelCoverage(
            availablePersonnel.Count,
            availableLongShifts,
            totalShiftDemand,
            requestDate,
            contextTitle: null,
            maxShiftsPerDay: maxShiftsPerDay,
            yesterdayNightDemand: yesterdayNightDemand);
    }
}
