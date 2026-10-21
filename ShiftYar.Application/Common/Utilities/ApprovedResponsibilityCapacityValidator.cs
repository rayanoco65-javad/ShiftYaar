using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.DepartmentModel.DepartmentEnums;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// اعتبارسنجی ظرفیت و سقف مجاز درخواست‌ها در دپارتمان‌های مسئولیت‌محور (ResponsibilityBased و Hybrid نظیر اتاق عمل).
/// این اعتبارسنجی‌ها تضمین می‌کنند که تأیید درخواست‌های حضور (ON) یا مرخصی (OFF) توسط سوپروایزر
/// منجر به کسر نیرو در شیفت‌ها یا کمبود پرسنل واجد شرایط برای نقش‌های تخصصی نشود.
/// </summary>
public static class ApprovedResponsibilityCapacityValidator
{
    public class ResponsibilityDemand
    {
        public int DepartmentResponsibilityId { get; set; }
        public string ResponsibilityTitle { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public int Priority { get; set; }
        public int RequiredTotalCount { get; set; }
        public int RequiredMaleCount { get; set; }
        public int RequiredFemaleCount { get; set; }
    }

    private class DemandSlot
    {
        public int RespId { get; set; }
        public bool IsDefault { get; set; }
        public UserGender? RequiredGender { get; set; }
        public int Priority { get; set; }
        public int OriginalOrder { get; set; }
    }

    /// <summary>
    /// آیا دپارتمان بر اساس StaffingMode یا وجود نقش‌های تعریف‌شده، مسئولیت‌محور است؟
    /// </summary>
    public static bool IsResponsibilityBased(
        DepartmentStaffingMode? mode,
        IEnumerable<Shift>? shifts = null,
        IEnumerable<DepartmentResponsibility>? responsibilities = null)
    {
        if (mode is DepartmentStaffingMode.ResponsibilityBased or DepartmentStaffingMode.Hybrid)
        {
            return true;
        }

        if (responsibilities != null && responsibilities.Any())
        {
            return true;
        }

        if (shifts != null && shifts.Any(s =>
            (s.RequiredResponsibilities != null && s.RequiredResponsibilities.Any()) ||
            (s.RequiredSpecialties != null && s.RequiredSpecialties.Any(rs => rs.DepartmentResponsibilityId.HasValue))))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// آیا کاربر صلاحیت انجام این مسئولیت را دارد؟ (نقش پیش‌فرض برای همه مجاز است؛ سایر نقش‌ها نیازمند انتساب است)
    /// </summary>
    public static bool CanUserPerformResponsibility(User? user, int respId, bool isDefault)
    {
        if (user == null) return false;
        if (isDefault) return true;
        if (user.UserResponsibilities == null) return false;
        return user.UserResponsibilities.Any(ur => ur.DepartmentResponsibilityId == respId);
    }

    /// <summary>
    /// استخراج تمام نیازمندی‌های نقش/مسئولیت یک شیفت در یک روز (با در نظر گرفتن تعطیل بودن یا نبودن روز)
    /// با ادغام هوشمند رکوردهای ShiftRequiredSpecialty (دارای DepartmentResponsibilityId) و ShiftRequiredResponsibility.
    /// </summary>
    public static List<ResponsibilityDemand> ResolveResponsibilityDemands(Shift shift, bool isHoliday)
    {
        var demands = new List<ResponsibilityDemand>();
        if (shift == null) return demands;

        // ۱. بارگذاری از ShiftRequiredSpecialties
        if (shift.RequiredSpecialties != null)
        {
            foreach (var rs in shift.RequiredSpecialties)
            {
                if (rs.DepartmentResponsibilityId.HasValue)
                {
                    var total = isHoliday
                        ? (rs.HolidayRequiredTottalCount ?? rs.RequiredTottalCount ?? 0)
                        : (rs.RequiredTottalCount ?? 0);
                    var male = isHoliday
                        ? (rs.HolidayRequiredMaleCount ?? rs.RequiredMaleCount ?? 0)
                        : (rs.RequiredMaleCount ?? 0);
                    var female = isHoliday
                        ? (rs.HolidayRequiredFemaleCount ?? rs.RequiredFemaleCount ?? 0)
                        : (rs.RequiredFemaleCount ?? 0);

                    demands.Add(new ResponsibilityDemand
                    {
                        DepartmentResponsibilityId = rs.DepartmentResponsibilityId.Value,
                        ResponsibilityTitle = rs.DepartmentResponsibility?.Title ?? string.Empty,
                        Priority = rs.DepartmentResponsibility?.Priority ?? 0,
                        IsDefault = rs.DepartmentResponsibility?.IsDefault ?? false,
                        RequiredTotalCount = total,
                        RequiredMaleCount = male,
                        RequiredFemaleCount = female
                    });
                }
            }
        }

        // ۲. بارگذاری مکمل از ShiftRequiredResponsibilities
        if (shift.RequiredResponsibilities != null)
        {
            foreach (var rr in shift.RequiredResponsibilities)
            {
                if (rr.DepartmentResponsibilityId.HasValue &&
                    !demands.Any(d => d.DepartmentResponsibilityId == rr.DepartmentResponsibilityId.Value))
                {
                    var total = isHoliday
                        ? (rr.HolidayRequiredTotalCount ?? rr.RequiredTotalCount ?? 0)
                        : (rr.RequiredTotalCount ?? 0);
                    var male = isHoliday
                        ? (rr.HolidayRequiredMaleCount ?? rr.RequiredMaleCount ?? 0)
                        : (rr.RequiredMaleCount ?? 0);
                    var female = isHoliday
                        ? (rr.HolidayRequiredFemaleCount ?? rr.RequiredFemaleCount ?? 0)
                        : (rr.RequiredFemaleCount ?? 0);

                    demands.Add(new ResponsibilityDemand
                    {
                        DepartmentResponsibilityId = rr.DepartmentResponsibilityId.Value,
                        ResponsibilityTitle = rr.DepartmentResponsibility?.Title ?? string.Empty,
                        Priority = rr.DepartmentResponsibility?.Priority ?? 0,
                        IsDefault = rr.DepartmentResponsibility?.IsDefault ?? false,
                        RequiredTotalCount = total,
                        RequiredMaleCount = male,
                        RequiredFemaleCount = female
                    });
                }
            }
        }

        return demands;
    }

    /// <summary>
    /// محاسبه ظرفیت کل یک شیفت در یک روز مشخص (مجموع نیاز تمام نقش‌ها + تخصص‌های عمومی شیفت)
    /// </summary>
    public static int ResolveShiftCapacity(Shift shift, bool isHoliday)
    {
        if (shift == null) return 0;
        var demands = ResolveResponsibilityDemands(shift, isHoliday);
        var respCapacity = demands.Sum(d => d.RequiredTotalCount);

        // اضافه کردن نیازهای عمومی بدون شناسه نقش در صورت وجود
        var generalReqs = shift.RequiredSpecialties?
            .Where(rs => !rs.DepartmentResponsibilityId.HasValue);
        if (generalReqs != null)
        {
            foreach (var gr in generalReqs)
            {
                respCapacity += isHoliday
                    ? (gr.HolidayRequiredTottalCount ?? gr.RequiredTottalCount ?? 0)
                    : (gr.RequiredTottalCount ?? 0);
            }
        }

        return respCapacity;
    }

    /// <summary>
    /// محاسبه بیشترین ظرفیت تعریف‌شده برای یک نوبت شیفت (Morning/Evening/Night) در بین شیفت‌های دپارتمان
    /// </summary>
    public static int ResolveShiftCapacity(IEnumerable<Shift> shifts, ShiftLabel shiftLabel, bool isHoliday)
    {
        int? capacity = null;
        foreach (var shift in shifts.Where(s => s.Label == shiftLabel))
        {
            var c = ResolveShiftCapacity(shift, isHoliday);
            capacity = capacity.HasValue ? Math.Max(capacity.Value, c) : c;
        }
        return capacity ?? 0;
    }

    /// <summary>
    /// مجموع کل پرسنل مورد نیاز تمام شیفت‌های دپارتمان در یک روز تقویمی (مجموع شیفت‌های امروز)
    /// </summary>
    public static int CalculateDailyShiftDemand(IEnumerable<Shift> shifts, bool isHoliday)
    {
        var total = 0;
        foreach (var shift in shifts)
        {
            total += ResolveShiftCapacity(shift, isHoliday);
        }
        return total;
    }

    /// <summary>
    /// مجموع پرسنل مورد نیاز شیفت شب در یک روز تقویمی (استراحت اجباری شیفت شب روز قبل)
    /// </summary>
    public static int CalculateNightShiftDemand(IEnumerable<Shift> shifts, bool isHoliday)
    {
        var total = 0;
        foreach (var shift in shifts)
        {
            var label = shift.Label ?? ShiftLabelResolver.InferLabelFromStartTime(shift);
            if (label == ShiftLabel.Night)
            {
                total += ResolveShiftCapacity(shift, isHoliday);
            }
        }
        return total;
    }

    /// <summary>
    /// مجموع نیاز یک مسئولیت خاص در بین شیفت‌های دپارتمان در یک روز مشخص
    /// </summary>
    public static int CalculateDemandForResponsibility(
        IEnumerable<Shift> shifts,
        int respId,
        bool isHoliday,
        ShiftLabel? labelFilter = null)
    {
        var total = 0;
        foreach (var shift in shifts)
        {
            if (labelFilter.HasValue && shift.Label != labelFilter.Value)
            {
                continue;
            }

            var demands = ResolveResponsibilityDemands(shift, isHoliday);
            var matching = demands.FirstOrDefault(d => d.DepartmentResponsibilityId == respId);
            if (matching != null)
            {
                total += matching.RequiredTotalCount;
            }
        }
        return total;
    }

    /// <summary>
    /// محاسبه سقف مجاز مرخصی روزانه در دپارتمان مسئولیت‌محور
    /// </summary>
    public static int CalculateMaxDailyLeaveCapacity(
        int totalActivePersonnel,
        int todayShiftDemand,
        int yesterdayNightShiftDemand)
    {
        var minRequired = todayShiftDemand + yesterdayNightShiftDemand;
        return Math.Max(0, totalActivePersonnel - minRequired);
    }

    /// <summary>
    /// اعتبارسنجی کامل تأیید درخواست حضور (ON) در شیفت مشخص برای دپارتمان مسئولیت‌محور:
    /// ۱. کنترل ظرفیت کل شیفت
    /// ۲. بررسی صلاحیت کاربر در نقش‌های مورد نیاز شیفت
    /// ۳. انطباق دوبخشی جهت اطمینان از تکمیل نشدن ظرفیت نقش‌های کاربر توسط سایر پرسنل تأییدشده
    /// </summary>
    public static string? ValidateOnShiftCapacity(
        IEnumerable<Shift> shifts,
        ShiftLabel shiftLabel,
        DateTime requestDate,
        bool isHoliday,
        User requestingUser,
        IEnumerable<DepartmentResponsibility> departmentResponsibilities,
        IEnumerable<ShiftRequest> approvedRequestsOnShift,
        IEnumerable<User> departmentUsers)
    {
        var matchingShifts = shifts.Where(s => s.Label == shiftLabel).ToList();
        if (matchingShifts.Count == 0)
        {
            return null;
        }

        var shiftCapacity = ResolveShiftCapacity(shifts, shiftLabel, isHoliday);
        if (shiftCapacity <= 0)
        {
            return null;
        }

        var approvedList = approvedRequestsOnShift.ToList();
        var approvedUserNames = approvedList
            .Select(r => FormatUserDisplayName(r.User))
            .ToList();

        // ۱. کنترل سقف کل شیفت
        if (approvedList.Count + 1 > shiftCapacity)
        {
            return ApprovedOnShiftCapacityValidator.BuildExceededCapacityMessage(
                approvedList.Count,
                shiftCapacity,
                shiftLabel,
                requestDate,
                approvedUserNames);
        }

        // ۲. استخراج نیازمندی‌های نقش شیفت
        var primaryShift = matchingShifts.OrderByDescending(s => ResolveShiftCapacity(s, isHoliday)).First();
        var shiftDemands = ResolveResponsibilityDemands(primaryShift, isHoliday);

        if (shiftDemands.Count == 0)
        {
            return null;
        }

        // بررسی اینکه آیا کاربر صلاحیت حداقل یکی از نقش‌های این شیفت را دارد؟
        var canPerformAny = shiftDemands.Any(d =>
            CanUserPerformResponsibility(requestingUser, d.DepartmentResponsibilityId, d.IsDefault));

        if (!canPerformAny)
        {
            var shiftName = ApprovedOnShiftCapacityValidator.GetShiftLabelDisplayName(shiftLabel);
            var reqTitles = shiftDemands
                .Select(d => !string.IsNullOrWhiteSpace(d.ResponsibilityTitle)
                    ? $"«{d.ResponsibilityTitle}»"
                    : $"«نقش {d.DepartmentResponsibilityId}»")
                .Distinct();
            var userName = FormatUserDisplayName(requestingUser);

            return $"کاربر {userName} صلاحیت هیچ‌یک از نقش‌های مورد نیاز شیفت {shiftName} ({string.Join("، ", reqTitles)}) را ندارد و امکان تأیید حضور ایشان در این شیفت وجود ندارد.";
        }

        // ۳. بررسی انطباق دوبخشی (Bipartite Matching) برای تمام پرسنل تأییدشده + کاربر متقاضی
        var usersMap = departmentUsers.GroupBy(u => u.Id ?? 0).ToDictionary(g => g.Key, g => g.First());
        if (requestingUser.Id.HasValue && !usersMap.ContainsKey(requestingUser.Id.Value))
        {
            usersMap[requestingUser.Id.Value] = requestingUser;
        }

        var allCandidates = new List<User>();
        foreach (var r in approvedList)
        {
            if (r.UserId.HasValue && usersMap.TryGetValue(r.UserId.Value, out var u))
            {
                allCandidates.Add(u);
            }
            else if (r.User != null)
            {
                allCandidates.Add(r.User);
            }
        }
        allCandidates.Add(requestingUser);

        // ساخت اسلات‌های تقاضای شیفت
        var demandSlots = new List<DemandSlot>();
        var order = 0;
        foreach (var req in shiftDemands)
        {
            for (var i = 0; i < req.RequiredMaleCount; i++)
            {
                demandSlots.Add(new DemandSlot
                {
                    RespId = req.DepartmentResponsibilityId,
                    IsDefault = req.IsDefault,
                    RequiredGender = UserGender.Male,
                    Priority = req.Priority,
                    OriginalOrder = order++
                });
            }
            for (var i = 0; i < req.RequiredFemaleCount; i++)
            {
                demandSlots.Add(new DemandSlot
                {
                    RespId = req.DepartmentResponsibilityId,
                    IsDefault = req.IsDefault,
                    RequiredGender = UserGender.Female,
                    Priority = req.Priority,
                    OriginalOrder = order++
                });
            }
            var remaining = Math.Max(0, req.RequiredTotalCount - req.RequiredMaleCount - req.RequiredFemaleCount);
            for (var i = 0; i < remaining; i++)
            {
                demandSlots.Add(new DemandSlot
                {
                    RespId = req.DepartmentResponsibilityId,
                    IsDefault = req.IsDefault,
                    RequiredGender = null,
                    Priority = req.Priority,
                    OriginalOrder = order++
                });
            }
        }

        // افزودن اسلات‌های تخصص عمومی در صورت وجود
        var extraSlots = Math.Max(0, shiftCapacity - demandSlots.Count);
        for (var i = 0; i < extraSlots; i++)
        {
            demandSlots.Add(new DemandSlot
            {
                RespId = 0,
                IsDefault = true,
                RequiredGender = null,
                Priority = 0,
                OriginalOrder = order++
            });
        }

        var orderedDemands = demandSlots
            .OrderBy(d => d.IsDefault ? 1 : 0)
            .ThenByDescending(d => d.Priority)
            .ThenBy(d => d.OriginalOrder)
            .ToList();

        bool CanAssign(DemandSlot d, User u)
        {
            if (d.RequiredGender.HasValue && u.Gender.HasValue && u.Gender.Value != d.RequiredGender.Value)
            {
                return false;
            }
            if (d.RespId == 0 && d.IsDefault)
            {
                return true;
            }
            return CanUserPerformResponsibility(u, d.RespId, d.IsDefault);
        }

        var matchDemandToAssignee = new Dictionary<int, int>();
        var matchAssigneeToDemand = new Dictionary<int, int>();

        bool TryDfs(int demandIdx, HashSet<int> visitedAssignees)
        {
            var demand = orderedDemands[demandIdx];
            var eligibleAssignees = Enumerable.Range(0, allCandidates.Count)
                .Where(aIdx => CanAssign(demand, allCandidates[aIdx]))
                .OrderBy(aIdx => (allCandidates[aIdx].UserResponsibilities?.Count ?? 0));

            foreach (var aIdx in eligibleAssignees)
            {
                if (visitedAssignees.Contains(aIdx)) continue;
                visitedAssignees.Add(aIdx);

                if (!matchAssigneeToDemand.TryGetValue(aIdx, out var prevDemandIdx) || TryDfs(prevDemandIdx, visitedAssignees))
                {
                    matchDemandToAssignee[demandIdx] = aIdx;
                    matchAssigneeToDemand[aIdx] = demandIdx;
                    return true;
                }
            }

            return false;
        }

        for (var dIdx = 0; dIdx < orderedDemands.Count; dIdx++)
        {
            var visited = new HashSet<int>();
            TryDfs(dIdx, visited);
        }

        if (matchAssigneeToDemand.Count < allCandidates.Count)
        {
            var shiftName = ApprovedOnShiftCapacityValidator.GetShiftLabelDisplayName(shiftLabel);
            var persianDate = DateConverter.ConvertToPersianDate(requestDate);
            var userName = FormatUserDisplayName(requestingUser);

            return $"ظرفیت نقش‌ها و مسئولیت‌های قابل پوشش توسط کاربر {userName} در شیفت {shiftName} تاریخ {persianDate} تکمیل شده است. " +
                   "تمام جایگاه‌های باقیمانده این شیفت نیازمند نقش‌های دیگری هستند که توسط این کاربر یا سایر پرسنل تأییدشده قابل پوشش نیستند " +
                   "و تأیید این درخواست منجر به کسر نیرو در نقش‌های تخصصی شیفت خواهد شد.";
        }

        return null;
    }

    /// <summary>
    /// اعتبارسنجی کامل تأیید درخواست مرخصی / عدم‌حضور (OFF) برای دپارتمان مسئولیت‌محور:
    /// ۱. کنترل سقف کل مرخصی روزانه دپارتمان
    /// ۲. کنترل سقف مرخصی به تفکیک تک‌تک نقش‌های تخصصی کاربر (جلوگیری از کسر نیرو در هر نقش)
    /// </summary>
    public static string? ValidateDailyLeaveCapacity(
        IEnumerable<Shift> shifts,
        DateTime requestDate,
        bool isHolidayToday,
        bool isHolidayYesterday,
        User requestingUser,
        IEnumerable<DepartmentResponsibility> departmentResponsibilities,
        IEnumerable<ShiftRequest> approvedLeaveRequestsOnDate,
        IEnumerable<User> departmentUsers)
    {
        var shiftsList = shifts.ToList();
        var usersList = departmentUsers.ToList();

        var todayShiftDemand = CalculateDailyShiftDemand(shiftsList, isHolidayToday);
        var yesterdayNightDemand = CalculateNightShiftDemand(shiftsList, isHolidayYesterday);
        var totalActivePersonnel = usersList.Count;

        var maxDailyLeaveCapacity = CalculateMaxDailyLeaveCapacity(
            totalActivePersonnel, todayShiftDemand, yesterdayNightDemand);

        var approvedLeaveList = approvedLeaveRequestsOnDate.ToList();
        var approvedUserIds = approvedLeaveList
            .Where(r => r.UserId.HasValue)
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToHashSet();

        var approvedDisplayNames = approvedLeaveList
            .Where(r => r.UserId.HasValue && approvedUserIds.Contains(r.UserId.Value))
            .GroupBy(r => r.UserId)
            .Select(g => FormatUserDisplayName(g.First().User))
            .ToList();

        // ۱. کنترل سقف کل مرخصی روزانه بخش
        if (ApprovedLeaveCapacityValidator.WouldExceedCapacity(approvedUserIds.Count, maxDailyLeaveCapacity))
        {
            return ApprovedLeaveCapacityValidator.BuildExceededCapacityMessage(
                approvedUserIds.Count,
                maxDailyLeaveCapacity,
                totalActivePersonnel,
                todayShiftDemand,
                yesterdayNightDemand,
                requestDate,
                specialtyName: null,
                approvedDisplayNames);
        }

        // ۲. کنترل سقف مرخصی به تفکیک نقش‌های تخصصی کاربر (نقش‌های غیرپیش‌فرض)
        var respList = departmentResponsibilities.ToList();
        foreach (var resp in respList.Where(r => r.Id.HasValue && !(r.IsDefault ?? false)))
        {
            var respId = resp.Id!.Value;

            // اگر کاربر متقاضی این مسئولیت را ندارد، اعطای مرخصی به او اثری روی این نقش ندارد
            if (!CanUserPerformResponsibility(requestingUser, respId, isDefault: false))
            {
                continue;
            }

            var todayDemandR = CalculateDemandForResponsibility(shiftsList, respId, isHolidayToday);
            var yesterdayNightDemandR = CalculateDemandForResponsibility(
                shiftsList, respId, isHolidayYesterday, ShiftLabel.Night);

            var minRequiredR = todayDemandR + yesterdayNightDemandR;
            if (minRequiredR <= 0)
            {
                continue;
            }

            // پرسنل فعال دارای صلاحیت این نقش
            var qualifiedUsersR = usersList
                .Where(u => CanUserPerformResponsibility(u, respId, isDefault: false))
                .ToList();

            var approvedLeavesR = qualifiedUsersR
                .Count(u => u.Id.HasValue && approvedUserIds.Contains(u.Id.Value));

            var maxLeaveR = Math.Max(0, qualifiedUsersR.Count - minRequiredR);

            if (approvedLeavesR + 1 > maxLeaveR)
            {
                var roleTitle = !string.IsNullOrWhiteSpace(resp.Title) ? resp.Title.Trim() : $"نقش {respId}";
                var persianDate = DateConverter.ConvertToPersianDate(requestDate);

                var approvedNamesForRole = qualifiedUsersR
                    .Where(u => u.Id.HasValue && approvedUserIds.Contains(u.Id.Value))
                    .Select(u => FormatUserDisplayName(u))
                    .ToList();
                var namesPart = approvedNamesForRole.Count > 0
                    ? $" ({string.Join("، ", approvedNamesForRole)})"
                    : string.Empty;

                if (maxLeaveR == 0)
                {
                    return $"به دلیل محدودیت نیرو برای مسئولیت «{roleTitle}» در تاریخ {persianDate}، امکان اعطای مرخصی وجود ندارد. " +
                           $"حداقل {minRequiredR} نفر نیروی واجد شرایط برای پوشش شیفت‌های امروز ({todayDemandR} نفر) و استراحت اجباری شیفت شب روز قبل ({yesterdayNightDemandR} نفر) الزامی است، " +
                           $"در حالی که کل پرسنل فعال این بخش با صلاحیت «{roleTitle}» {qualifiedUsersR.Count} نفر است.";
                }

                return $"ظرفیت مرخصی برای مسئولیت «{roleTitle}» در تاریخ {persianDate} تکمیل شده است. " +
                       $"حداکثر سقف مجاز مرخصی همزمان برای این نقش {maxLeaveR} نفر است " +
                       $"(کل پرسنل دارای این مسئولیت: {qualifiedUsersR.Count} نفر، نیاز شیفت‌های امروز: {todayDemandR} نفر، استراحت شب قبل: {yesterdayNightDemandR} نفر). " +
                       $"در حال حاضر {approvedLeavesR} مرخصی تأییدشده برای این مسئولیت{namesPart} وجود دارد. " +
                       $"امکان تأیید مرخصی جدید وجود ندارد زیرا منجر به کسر نیرو برای مسئولیت «{roleTitle}» خواهد شد.";
            }
        }

        return null;
    }

    private static string FormatUserDisplayName(User? user)
    {
        if (user == null) return "کاربر نامشخص";
        if (!string.IsNullOrWhiteSpace(user.FullName)) return user.FullName.Trim();
        return user.Id.HasValue ? $"کاربر {user.Id.Value}" : "کاربر نامشخص";
    }
}
