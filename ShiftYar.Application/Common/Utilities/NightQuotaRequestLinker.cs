using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Enums.ShiftModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// هماهنگی سهمیه شب ماهانه با درخواست‌های تأییدشدهٔ شیفت شب:
/// هر درخواست شب تأییدشده یک واحد از سهمیه کاربر مصرف می‌کند
/// و مجموع سهمیه‌ها نباید از تعداد شب‌های ماه بیشتر شود.
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
                $"برای ثبت/تأیید درخواست شیفت شب، ابتدا سهمیه شب ماهانه کاربر برای {persianYear}/{persianMonth:00} باید تعیین شود.";
        }

        if (approvedNightCountInMonth + pendingAdditional > exactNightQuota.Value)
        {
            return
                $"سهمیه شیفت شب این کاربر در {persianYear}/{persianMonth:00} برابر {exactNightQuota.Value} است " +
                $"و هم‌اکنون {approvedNightCountInMonth} درخواست شب تأییدشده دارد. " +
                "امکان ثبت درخواست شب بیشتر از سهمیه وجود ندارد.";
        }

        return null;
    }

    public static string? ValidateHolidayNightOnAgainstQuota(
        int approvedHolidayNightCountInMonth,
        int? exactHolidayNightQuota,
        int persianYear,
        int persianMonth,
        int pendingAdditional = 1)
    {
        if (!exactHolidayNightQuota.HasValue)
        {
            return null; // سهمیه تعطیل اختیاری است
        }

        if (approvedHolidayNightCountInMonth + pendingAdditional > exactHolidayNightQuota.Value)
        {
            return
                $"سهمیه شب تعطیل/آخرهفته این کاربر در {persianYear}/{persianMonth:00} برابر {exactHolidayNightQuota.Value} است " +
                $"و هم‌اکنون {approvedHolidayNightCountInMonth} درخواست شب تعطیل تأییدشده دارد.";
        }

        return null;
    }

    public static string? ValidateQuotaNotBelowApprovedRequests(
        int? newNightQuota,
        int approvedNightCount,
        int? newHolidayQuota,
        int approvedHolidayNightCount,
        int persianYear,
        int persianMonth)
    {
        if (newNightQuota.HasValue && newNightQuota.Value < approvedNightCount)
        {
            return
                $"سهمیه شب نمی‌تواند کمتر از تعداد درخواست‌های شب تأییدشده ({approvedNightCount}) " +
                $"برای {persianYear}/{persianMonth:00} باشد.";
        }

        if (newHolidayQuota.HasValue && newHolidayQuota.Value < approvedHolidayNightCount)
        {
            return
                $"سهمیه شب تعطیل نمی‌تواند کمتر از تعداد درخواست‌های شب تعطیل تأییدشده ({approvedHolidayNightCount}) " +
                $"برای {persianYear}/{persianMonth:00} باشد.";
        }

        return null;
    }
}
