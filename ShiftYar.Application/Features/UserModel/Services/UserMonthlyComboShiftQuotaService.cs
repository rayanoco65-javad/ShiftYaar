using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Features.UserModel.Services;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.UserModel;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Features.UserModel.Services
{
    public class UserMonthlyComboShiftQuotaService : IUserMonthlyComboShiftQuotaService
    {
        private readonly IEfRepository<UserMonthlyComboShiftQuota> _repository;
        private readonly IEfRepository<User> _userRepository;
        private readonly IEfRepository<ShiftDate> _shiftDateRepository;
        private readonly ILogger<UserMonthlyComboShiftQuotaService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserMonthlyComboShiftQuotaService(
            IEfRepository<UserMonthlyComboShiftQuota> repository,
            IEfRepository<User> userRepository,
            IEfRepository<ShiftDate> shiftDateRepository,
            ILogger<UserMonthlyComboShiftQuotaService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _userRepository = userRepository;
            _shiftDateRepository = shiftDateRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<UserMonthlyComboShiftQuotaDtoGet>>> GetQuotasAsync(
            UserMonthlyComboShiftQuotaFilter filter)
        {
            var result = await _repository.GetByFilterAsync(filter, "User");
            var items = result.Items.Select(MapToDto).ToList();
            var paged = new PagedResponse<UserMonthlyComboShiftQuotaDtoGet>
            {
                Items = items,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)Math.Max(1, filter.PageSize))
            };
            return ApiResponse<PagedResponse<UserMonthlyComboShiftQuotaDtoGet>>.Success(paged);
        }

        public async Task<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>> GetQuotaAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id, "User");
            return entity == null
                ? ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail("سهمیه ترکیبی یافت نشد.")
                : ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Success(MapToDto(entity));
        }

        public async Task<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>> GetQuotaByUserMonthAsync(
            int userId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var error))
            {
                return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail(error);
            }

            var (items, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyComboShiftQuota>(q =>
                    q.UserId == userId && q.PersianYear == persianYear && q.PersianMonth == persianMonth),
                "User");

            var entity = items.FirstOrDefault();
            return entity == null
                ? ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail("برای این کاربر در ماه موردنظر سهمیه ترکیبی ثبت نشده است.")
                : ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Success(MapToDto(entity));
        }

        public async Task<ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>> GetDepartmentMonthQuotasAsync(
            int departmentId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var error))
            {
                return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail(error);
            }

            var filter = new UserMonthlyComboShiftQuotaFilter
            {
                DepartmentId = departmentId,
                PersianYear = persianYear,
                PersianMonth = persianMonth,
                PageSize = 500
            };
            var result = await _repository.GetByFilterAsync(filter, "User");
            return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Success(result.Items.Select(MapToDto).ToList());
        }

        public async Task<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>> UpsertQuotaAsync(UserMonthlyComboShiftQuotaDtoAdd dto)
        {
            if (!IsValidMonth(dto.PersianYear, dto.PersianMonth, out var monthError))
            {
                return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail(monthError);
            }

            if (!TryNormalize(dto, out var values, out var countError))
            {
                return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail(countError);
            }

            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user == null)
            {
                return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail("کاربر یافت نشد.");
            }

            if (!user.DepartmentId.HasValue)
            {
                return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail("کاربر به دپارتمانی متصل نیست.");
            }

            var permissions = ShiftEligibilityResolver.ResolvePermissions(
                user.AllowedShiftPermissions,
                user.ShiftType ?? ShiftTypes.FixedShift,
                user.ShiftSubType ?? ShiftSubTypes.FixedMorning,
                user.TwoShiftRotationPattern);

            var permissionError = ComboShiftQuotaPermissionValidator.Validate(
                user, permissions,
                values.MorningEveningShiftCount, values.MorningEveningFallbackParticipation,
                values.MorningEveningHolidayCount, values.MorningEveningHolidayFallback,
                values.MorningNightShiftCount, values.MorningNightFallbackParticipation,
                values.MorningNightHolidayCount, values.MorningNightHolidayFallback);
            if (permissionError != null)
            {
                return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail(permissionError);
            }

            if (ComboShiftQuotaPermissionValidator.HasAnyConfiguredValue(
                    values.MorningEveningShiftCount, values.MorningEveningFallbackParticipation,
                    values.MorningEveningHolidayCount, values.MorningEveningHolidayFallback,
                    values.MorningNightShiftCount, values.MorningNightFallbackParticipation,
                    values.MorningNightHolidayCount, values.MorningNightHolidayFallback))
            {
                var monthLimits = await TryGetMonthDayCapacitiesAsync(user.DepartmentId.Value, dto.PersianYear, dto.PersianMonth);
                if (monthLimits.Error != null)
                {
                    return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail(monthLimits.Error);
                }

                var limitError = UserMonthlyComboShiftQuotaLimits.Validate(
                    values.MorningEveningShiftCount,
                    values.MorningEveningHolidayCount,
                    values.MorningNightShiftCount,
                    values.MorningNightHolidayCount,
                    monthLimits.MonthDays,
                    monthLimits.HolidayDays,
                    dto.PersianYear,
                    dto.PersianMonth);
                if (limitError != null)
                {
                    return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Fail(limitError);
                }
            }

            return await PersistUpsertAsync(dto, user, values);
        }

        public async Task<ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>> UpsertBulkAsync(
            UserMonthlyComboShiftQuotaBulkUpsertDto dto)
        {
            if (!IsValidMonth(dto.PersianYear, dto.PersianMonth, out var monthError))
            {
                return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail(monthError);
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail("لیست سهمیه‌ها خالی است.");
            }

            var (deptUsers, _) = await _userRepository.GetByFilterAsync(
                new SimpleFilter<User>(u => u.DepartmentId == dto.DepartmentId && u.IsActive == true));
            var deptUserById = deptUsers.Where(u => u.Id.HasValue).ToDictionary(u => u.Id!.Value);

            var invalidUser = dto.Items.FirstOrDefault(i => !deptUserById.ContainsKey(i.UserId));
            if (invalidUser != null)
            {
                return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail(
                    $"کاربر {invalidUser.UserId} در دپارتمان {dto.DepartmentId} یافت نشد.");
            }

            var monthLimits = await TryGetMonthDayCapacitiesAsync(dto.DepartmentId, dto.PersianYear, dto.PersianMonth);
            if (monthLimits.Error != null)
            {
                return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail(monthLimits.Error);
            }

            var overrides = new Dictionary<int, ComboQuotaValues>();
            foreach (var item in dto.Items)
            {
                if (!TryNormalize(item, out var values, out var countError))
                {
                    return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail($"کاربر {item.UserId}: {countError}");
                }

                var user = deptUserById[item.UserId];
                var permissions = ShiftEligibilityResolver.ResolvePermissions(
                    user.AllowedShiftPermissions,
                    user.ShiftType ?? ShiftTypes.FixedShift,
                    user.ShiftSubType ?? ShiftSubTypes.FixedMorning,
                    user.TwoShiftRotationPattern);

                var permissionError = ComboShiftQuotaPermissionValidator.Validate(
                    user, permissions,
                    values.MorningEveningShiftCount, values.MorningEveningFallbackParticipation,
                    values.MorningEveningHolidayCount, values.MorningEveningHolidayFallback,
                    values.MorningNightShiftCount, values.MorningNightFallbackParticipation,
                    values.MorningNightHolidayCount, values.MorningNightHolidayFallback);
                if (permissionError != null)
                {
                    return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail($"کاربر {item.UserId}: {permissionError}");
                }

                if (ComboShiftQuotaPermissionValidator.HasAnyConfiguredValue(
                        values.MorningEveningShiftCount, values.MorningEveningFallbackParticipation,
                        values.MorningEveningHolidayCount, values.MorningEveningHolidayFallback,
                        values.MorningNightShiftCount, values.MorningNightFallbackParticipation,
                        values.MorningNightHolidayCount, values.MorningNightHolidayFallback))
                {
                    var limitError = UserMonthlyComboShiftQuotaLimits.Validate(
                        values.MorningEveningShiftCount,
                        values.MorningEveningHolidayCount,
                        values.MorningNightShiftCount,
                        values.MorningNightHolidayCount,
                        monthLimits.MonthDays,
                        monthLimits.HolidayDays,
                        dto.PersianYear,
                        dto.PersianMonth);
                    if (limitError != null)
                    {
                        return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail($"کاربر {item.UserId}: {limitError}");
                    }
                }

                overrides[item.UserId] = values;
            }

            var results = new List<UserMonthlyComboShiftQuotaDtoGet>();
            foreach (var item in dto.Items)
            {
                var values = overrides[item.UserId];
                var upsert = await PersistUpsertAsync(
                    new UserMonthlyComboShiftQuotaDtoAdd
                    {
                        UserId = item.UserId,
                        PersianYear = dto.PersianYear,
                        PersianMonth = dto.PersianMonth,
                        MorningEveningShiftCount = values.MorningEveningShiftCount,
                        MorningEveningFallbackParticipation = values.MorningEveningFallbackParticipation,
                        MorningEveningHolidayCount = values.MorningEveningHolidayCount,
                        MorningEveningHolidayFallback = values.MorningEveningHolidayFallback,
                        MorningNightShiftCount = values.MorningNightShiftCount,
                        MorningNightFallbackParticipation = values.MorningNightFallbackParticipation,
                        MorningNightHolidayCount = values.MorningNightHolidayCount,
                        MorningNightHolidayFallback = values.MorningNightHolidayFallback
                    },
                    deptUserById[item.UserId],
                    values);

                if (!upsert.IsSuccess)
                {
                    return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Fail($"خطا برای کاربر {item.UserId}: {upsert.Message}");
                }

                if (upsert.Data != null && upsert.Data.Id > 0)
                {
                    results.Add(upsert.Data);
                }
            }

            return ApiResponse<List<UserMonthlyComboShiftQuotaDtoGet>>.Success(results, "سهمیه‌های ترکیبی ذخیره شدند.");
        }

        public async Task<ApiResponse<string>> DeleteQuotaAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                return ApiResponse<string>.Fail("سهمیه ترکیبی یافت نشد.");
            }

            _repository.Delete(entity);
            await _repository.SaveAsync();
            return ApiResponse<string>.Success("حذف شد.", "سهمیه ترکیبی حذف شد.");
        }

        private async Task<ApiResponse<UserMonthlyComboShiftQuotaDtoGet>> PersistUpsertAsync(
            UserMonthlyComboShiftQuotaDtoAdd dto,
            User user,
            ComboQuotaValues values)
        {
            var (existing, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyComboShiftQuota>(q =>
                    q.UserId == dto.UserId &&
                    q.PersianYear == dto.PersianYear &&
                    q.PersianMonth == dto.PersianMonth));

            var entity = existing.FirstOrDefault();
            var now = DateTime.Now;
            var actorId = GetActorUserId();
            var hasAny = ComboShiftQuotaPermissionValidator.HasAnyConfiguredValue(
                values.MorningEveningShiftCount, values.MorningEveningFallbackParticipation,
                values.MorningEveningHolidayCount, values.MorningEveningHolidayFallback,
                values.MorningNightShiftCount, values.MorningNightFallbackParticipation,
                values.MorningNightHolidayCount, values.MorningNightHolidayFallback);

            if (entity == null)
            {
                if (!hasAny)
                {
                    return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Success(
                        new UserMonthlyComboShiftQuotaDtoGet
                        {
                            UserId = dto.UserId,
                            PersianYear = dto.PersianYear,
                            PersianMonth = dto.PersianMonth
                        },
                        "سهمیه ترکیبی برای این کاربر تنظیم نشده است.");
                }

                entity = new UserMonthlyComboShiftQuota
                {
                    UserId = dto.UserId,
                    PersianYear = dto.PersianYear,
                    PersianMonth = dto.PersianMonth,
                    CreateDate = now,
                    TheUserId = actorId
                };
                ApplyValues(entity, values);
                await _repository.AddAsync(entity);
            }
            else
            {
                if (!hasAny)
                {
                    _repository.Delete(entity);
                    await _repository.SaveAsync();
                    return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Success(
                        new UserMonthlyComboShiftQuotaDtoGet
                        {
                            UserId = dto.UserId,
                            PersianYear = dto.PersianYear,
                            PersianMonth = dto.PersianMonth
                        },
                        "سهمیه ترکیبی حذف شد.");
                }

                ApplyValues(entity, values);
                entity.UpdateDate = now;
                entity.TheUserId = actorId;
                _repository.Update(entity);
            }

            await _repository.SaveAsync();
            entity.User = user;
            return ApiResponse<UserMonthlyComboShiftQuotaDtoGet>.Success(MapToDto(entity), "سهمیه ترکیبی ذخیره شد.");
        }

        private static void ApplyValues(UserMonthlyComboShiftQuota entity, ComboQuotaValues values)
        {
            entity.MorningEveningShiftCount = values.MorningEveningShiftCount;
            entity.MorningEveningFallbackParticipation = values.MorningEveningFallbackParticipation;
            entity.MorningEveningHolidayCount = values.MorningEveningHolidayCount;
            entity.MorningEveningHolidayFallback = values.MorningEveningHolidayFallback;
            entity.MorningNightShiftCount = values.MorningNightShiftCount;
            entity.MorningNightFallbackParticipation = values.MorningNightFallbackParticipation;
            entity.MorningNightHolidayCount = values.MorningNightHolidayCount;
            entity.MorningNightHolidayFallback = values.MorningNightHolidayFallback;
        }

        private async Task<(int MonthDays, int HolidayDays, string? Error)> TryGetMonthDayCapacitiesAsync(
            int departmentId, int persianYear, int persianMonth)
        {
            var (monthStart, monthEnd, daysInMonth) = PersianMonthDayCalendar.GetMonthBounds(persianYear, persianMonth);
            var (shiftDates, _) = await _shiftDateRepository.GetByFilterAsync(
                new SimpleFilter<ShiftDate>(d =>
                    d.Date != null &&
                    d.Date >= monthStart &&
                    d.Date <= monthEnd));

            var monthDates = shiftDates
                .Where(d => d.Date.HasValue)
                .Select(d => d.Date!.Value.Date)
                .Distinct()
                .ToList();

            if (monthDates.Count == 0)
            {
                return (0, 0, $"در ShiftDates برای ماه شمسی {persianYear}/{persianMonth:00} هیچ روزی ثبت نشده است.");
            }

            if (monthDates.Count < daysInMonth)
            {
                return (0, 0,
                    $"تقویم ShiftDates برای ماه {persianYear}/{persianMonth:00} ناقص است ({monthDates.Count} از {daysInMonth} روز).");
            }

            var holidayDates = shiftDates
                .Where(d => d.IsHoliday == true && d.Date.HasValue)
                .Select(d => d.Date!.Value.Date)
                .ToHashSet();

            var (days, holidayDays) = PersianMonthDayCalendar.CountDayCapacities(monthDates, holidayDates);
            _ = departmentId;
            return (days, holidayDays, null);
        }

        private static UserMonthlyComboShiftQuotaDtoGet MapToDto(UserMonthlyComboShiftQuota entity) => new()
        {
            Id = entity.Id ?? 0,
            UserId = entity.UserId,
            UserName = entity.User?.FullName,
            DepartmentId = entity.User?.DepartmentId,
            PersianYear = entity.PersianYear,
            PersianMonth = entity.PersianMonth,
            MorningEveningShiftCount = entity.MorningEveningShiftCount,
            MorningEveningFallbackParticipation = entity.MorningEveningFallbackParticipation,
            MorningEveningHolidayCount = entity.MorningEveningHolidayCount,
            MorningEveningHolidayFallback = entity.MorningEveningHolidayFallback,
            MorningNightShiftCount = entity.MorningNightShiftCount,
            MorningNightFallbackParticipation = entity.MorningNightFallbackParticipation,
            MorningNightHolidayCount = entity.MorningNightHolidayCount,
            MorningNightHolidayFallback = entity.MorningNightHolidayFallback
        };

        private readonly record struct ComboQuotaValues(
            int? MorningEveningShiftCount,
            bool? MorningEveningFallbackParticipation,
            int? MorningEveningHolidayCount,
            bool? MorningEveningHolidayFallback,
            int? MorningNightShiftCount,
            bool? MorningNightFallbackParticipation,
            int? MorningNightHolidayCount,
            bool? MorningNightHolidayFallback);

        private static bool TryNormalize(UserMonthlyComboShiftQuotaDtoAdd dto, out ComboQuotaValues values, out string error) =>
            TryNormalize(
                dto.MorningEveningShiftCount, dto.MorningEveningFallbackParticipation,
                dto.MorningEveningHolidayCount, dto.MorningEveningHolidayFallback,
                dto.MorningNightShiftCount, dto.MorningNightFallbackParticipation,
                dto.MorningNightHolidayCount, dto.MorningNightHolidayFallback,
                out values, out error);

        private static bool TryNormalize(UserMonthlyComboShiftQuotaItemDto item, out ComboQuotaValues values, out string error) =>
            TryNormalize(
                item.MorningEveningShiftCount, item.MorningEveningFallbackParticipation,
                item.MorningEveningHolidayCount, item.MorningEveningHolidayFallback,
                item.MorningNightShiftCount, item.MorningNightFallbackParticipation,
                item.MorningNightHolidayCount, item.MorningNightHolidayFallback,
                out values, out error);

        private static bool TryNormalize(
            int? meCount, bool? meFallback, int? meHoliday, bool? meHolidayFallback,
            int? mnCount, bool? mnFallback, int? mnHoliday, bool? mnHolidayFallback,
            out ComboQuotaValues values, out string error)
        {
            error = string.Empty;
            values = default;

            foreach (var (val, name) in new (int? Value, string Name)[]
                     {
                         (meCount, "صبح/عصر"), (meHoliday, "تعطیل صبح/عصر"),
                         (mnCount, "صبح/شب"), (mnHoliday, "تعطیل صبح/شب")
                     })
            {
                if (val.HasValue && val.Value < 0)
                {
                    error = $"تعداد سهمیه {name} نمی‌تواند منفی باشد.";
                    return false;
                }
            }

            if (meCount.HasValue && meHoliday.HasValue && meHoliday.Value > meCount.Value)
            {
                error = "سهمیه تعطیل صبح/عصر نمی‌تواند بیشتر از سهمیه کل صبح/عصر باشد.";
                return false;
            }

            if (mnCount.HasValue && mnHoliday.HasValue && mnHoliday.Value > mnCount.Value)
            {
                error = "سهمیه تعطیل صبح/شب نمی‌تواند بیشتر از سهمیه کل صبح/شب باشد.";
                return false;
            }

            values = new ComboQuotaValues(
                meCount, meFallback, meHoliday, meHolidayFallback,
                mnCount, mnFallback, mnHoliday, mnHolidayFallback);
            return true;
        }

        private static bool IsValidMonth(int year, int month, out string error)
        {
            if (year < 1300 || year > 1500) { error = "سال شمسی نامعتبر است."; return false; }
            if (month < 1 || month > 12) { error = "ماه شمسی باید بین ۱ تا ۱۲ باشد."; return false; }
            error = string.Empty;
            return true;
        }

        private int? GetActorUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
