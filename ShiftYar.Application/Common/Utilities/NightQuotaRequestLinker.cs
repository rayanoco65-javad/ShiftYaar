using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Enums.ShiftModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// هماهنگی دوطرفهٔ سهمیه شب ماهانه با درخواست‌های تأییدشدهٔ شیفت شب.
/// ExactNight سقف/کف کل شب‌های ماه است؛ ExactHoliday فقط حداقل شب تعطیل/آخرهفته است (نه سقف).
/// درخواست‌های شب تعطیل بیش از حداقل تعطیل مجازند تا وقتی از ExactNight فراتر نروند.
/// </summary>
public static class NightQuotaRequestLinker
{
    public static (int Year, int Month) GetPersianYearMonth(DateTime date)
    {
        var pc = new PersianCalendar();
        return (pc.GetYear(date.Date), pc.GetMonth(date.Date));
    }

    public static bool IsNightOnRequest(ShiftRequest request)
    {
        return request.RequestAction == RequestAction.RequestToBeOnShift &&
               request.RequestType == RequestType.SpecificShift &&
               request.ShiftLabel == ShiftEnums.ShiftLabel.Night;
    }

    public static int CountApprovedNightOnRequestsInMonth(
        IEnumerable<ShiftRequest> requests,
        int userId,
        int persianYear,
        int persianMonth,
        int? excludeRequestId = null)
    {
        var pc = new PersianCalendar();
        return requests.Count(r =>
            r.UserId == userId &&
            r.Status == RequestStatus.Approved &&
            IsNightOnRequest(r) &&
            r.RequestDate.HasValue &&
            pc.GetYear(r.RequestDate.Value) == persianYear &&
            pc.GetMonth(r.RequestDate.Value) == persianMonth &&
            (!excludeRequestId.HasValue || r.Id != excludeRequestId.Value));
    }

    public static int CountApprovedHolidayNightOnRequestsInMonth(
        IEnumerable<ShiftRequest> requests,
        int userId,
        int persianYear,
        int persianMonth,
        Func<DateTime, bool> isHolidayWeekendNight,
        int? excludeRequestId = null)
    {
        var pc = new PersianCalendar();
        return requests.Count(r =>
            r.UserId == userId &&
            r.Status == RequestStatus.Approved &&
            IsNightOnRequest(r) &&
            r.RequestDate.HasValue &&
            pc.GetYear(r.RequestDate.Value) == persianYear &&
            pc.GetMonth(r.RequestDate.Value) == persianMonth &&
            isHolidayWeekendNight(r.RequestDate.Value.Date) &&
            (!excludeRequestId.HasValue || r.Id != excludeRequestId.Value));
    }

    public static int CountApprovedNonHolidayNightOnRequestsInMonth(
        IEnumerable<ShiftRequest> requests,
        int userId,
        int persianYear,
        int persianMonth,
        Func<DateTime, bool> isHolidayWeekendNight,
        int? excludeRequestId = null)
    {
        var pc = new PersianCalendar();
        return requests.Count(r =>
            r.UserId == userId &&
            r.Status == RequestStatus.Approved &&
            IsNightOnRequest(r) &&
            r.RequestDate.HasValue &&
            pc.GetYear(r.RequestDate.Value) == persianYear &&
            pc.GetMonth(r.RequestDate.Value) == persianMonth &&
            !isHolidayWeekendNight(r.RequestDate.Value.Date) &&
            (!excludeRequestId.HasValue || r.Id != excludeRequestId.Value));
    }

    /// <summary>
    /// هنگام ثبت/تأیید درخواست شب: سهمیه کل ماه باید وجود داشته باشد و پر نشود.
    /// </summary>
    public static string? ValidateNightOnAgainstQuota(
        int approvedNightCountInMonth,
        int? exactNightQuota,
        int persianYear,
        int persianMonth,
        int pendingAdditional = 1)
    {
        if (!exactNightQuota.HasValue)
        {
            return
                $"امکان تأیید/ثبت درخواست شیفت شب وجود ندارد: برای کاربر در ماه {persianYear}/{persianMonth:00} " +
                "سهمیه شیفت شب تعیین نشده است. ابتدا سهمیه شب ماهانه را ثبت کنید.";
        }

        if (approvedNightCountInMonth + pendingAdditional > exactNightQuota.Value)
        {
            return
                $"امکان تأیید/ثبت این درخواست شیفت شب وجود ندارد: سهمیه شب کاربر در {persianYear}/{persianMonth:00} " +
                $"برابر {exactNightQuota.Value} است و هم‌اکنون {approvedNightCountInMonth} درخواست شب تأییدشده دارد " +
                $"(با این درخواست: {approvedNightCountInMonth + pendingAdditional}).";
        }

        return null;
    }

    /// <summary>
    /// سهمیه تعطیل/آخرهفته حداقل است، نه سقف.
    /// درخواست شب تعطیل فقط با سقف کل ExactNight محدود می‌شود (که قبلاً چک شده).
    /// </summary>
    public static string? ValidateHolidayNightOnAgainstQuota(
        int approvedHolidayNightCountInMonth,
        int? exactHolidayNightQuota,
        int persianYear,
        int persianMonth,
        int pendingAdditional = 1)
    {
        _ = approvedHolidayNightCountInMonth;
        _ = exactHolidayNightQuota;
        _ = persianYear;
        _ = persianMonth;
        _ = pendingAdditional;
        return null;
    }

    /// <summary>
    /// اگر حداقل شب تعطیل وجود دارد، درخواست‌های شب غیرتعطیل نباید جا برای آن باقی نگذارند.
    /// مثال: ExactNight=5 و ExactHoliday=1 ⇒ حداکثر ۴ درخواست شب غیرتعطیل.
    /// </summary>
    public static string? ValidateNonHolidayOnLeavesRoomForHoliday(
        int approvedNonHolidayNightCountInMonth,
        int? exactNightQuota,
        int? exactHolidayNightQuota,
        int persianYear,
        int persianMonth,
        int pendingAdditionalNonHoliday = 1,
        bool forQuotaUpsert = false)
    {
        if (!exactNightQuota.HasValue || !exactHolidayNightQuota.HasValue || exactHolidayNightQuota.Value <= 0)
        {
            return null;
        }

        var maxNonHoliday = Math.Max(0, exactNightQuota.Value - exactHolidayNightQuota.Value);
        var projected = approvedNonHolidayNightCountInMonth + pendingAdditionalNonHoliday;

        if (projected <= maxNonHoliday)
        {
            return null;
        }

        if (forQuotaUpsert)
        {
            return
                $"تنظیم سهمیه برای {persianYear}/{persianMonth:00} با درخواست‌های تأییدشده سازگار نیست: " +
                $"با سهمیه شب {exactNightQuota.Value} و سهمیه تعطیل/آخرهفته {exactHolidayNightQuota.Value} " +
                $"حداکثر {maxNonHoliday} شب غیرتعطیل مجاز است، ولی {approvedNonHolidayNightCountInMonth} درخواست شب غیرتعطیل تأییدشده وجود دارد. " +
                "سهمیه را افزایش دهید، سهمیه تعطیل را کاهش دهید، یا تاریخ/وضعیت درخواست‌ها را اصلاح کنید.";
        }

        return
            $"امکان تأیید/ثبت این درخواست شب غیرتعطیل وجود ندارد: با سهمیه شب {exactNightQuota.Value} و " +
            $"سهمیه شب تعطیل/آخرهفته {exactHolidayNightQuota.Value} در {persianYear}/{persianMonth:00}، " +
            $"حداکثر {maxNonHoliday} درخواست شب غیرتعطیل مجاز است " +
            $"(تأییدشده: {approvedNonHolidayNightCountInMonth}؛ با این درخواست: {projected}). " +
            "تاریخ را به شب تعطیل/آخرهفته تغییر دهید یا سهمیه را اصلاح کنید.";
    }

    /// <summary>
    /// هنگام تعیین/تغییر سهمیه: ExactNight نباید کمتر از کل درخواست‌های شب تأییدشده باشد
    /// و درخواست‌های غیرتعطیل باید جا برای حداقل تعطیل بگذارند.
    /// ExactHoliday حداقل است؛ می‌تواند کمتر از تعداد درخواست‌های شب تعطیل تأییدشده باشد.
    /// </summary>
    public static string? ValidateQuotaAgainstApprovedNightRequests(
        int? newNightQuota,
        int approvedNightCount,
        int? newHolidayQuota,
        int approvedHolidayNightCount,
        int approvedNonHolidayNightCount,
        int persianYear,
        int persianMonth)
    {
        if (!newNightQuota.HasValue && approvedNightCount > 0)
        {
            return
                $"نمی‌توان سهمیه شب را برای {persianYear}/{persianMonth:00} حذف کرد: " +
                $"{approvedNightCount} درخواست شیفت شب تأییدشده برای این کاربر در این ماه وجود دارد. " +
                "ابتدا وضعیت آن درخواست‌ها را تغییر دهید یا سهمیه را حداقل برابر تعداد آن‌ها نگه دارید.";
        }

        if (newNightQuota.HasValue && newNightQuota.Value < approvedNightCount)
        {
            return
                $"سهمیه شب برای {persianYear}/{persianMonth:00} نمی‌تواند کمتر از تعداد درخواست‌های شب تأییدشده " +
                $"({approvedNightCount}) باشد. مقدار پیشنهادی: {newNightQuota.Value}.";
        }

        _ = approvedHolidayNightCount;

        return ValidateNonHolidayOnLeavesRoomForHoliday(
            approvedNonHolidayNightCount,
            newNightQuota,
            newHolidayQuota,
            persianYear,
            persianMonth,
            pendingAdditionalNonHoliday: 0,
            forQuotaUpsert: true);
    }

    /// <summary>سازگاری عقب‌رو با فراخوانی‌های قبلی.</summary>
    public static string? ValidateQuotaNotBelowApprovedRequests(
        int? newNightQuota,
        int approvedNightCount,
        int? newHolidayQuota,
        int approvedHolidayNightCount,
        int persianYear,
        int persianMonth)
    {
        var approvedNonHoliday = Math.Max(0, approvedNightCount - approvedHolidayNightCount);
        return ValidateQuotaAgainstApprovedNightRequests(
            newNightQuota,
            approvedNightCount,
            newHolidayQuota,
            approvedHolidayNightCount,
            approvedNonHoliday,
            persianYear,
            persianMonth);
    }
}
