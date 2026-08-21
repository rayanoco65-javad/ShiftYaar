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
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.UserModel.Services
{
    public class UserMonthlyDayShiftQuotaService : IUserMonthlyDayShiftQuotaService
    {
        private readonly IEfRepository<UserMonthlyDayShiftQuota> _repository;
        private readonly IEfRepository<User> _userRepository;
        private readonly IEfRepository<ShiftDate> _shiftDateRepository;
        private readonly IEfRepository<Shift> _shiftRepository;
        private readonly ILogger<UserMonthlyDayShiftQuotaService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserMonthlyDayShiftQuotaService(
            IEfRepository<UserMonthlyDayShiftQuota> repository,
            IEfRepository<User> userRepository,
            IEfRepository<ShiftDate> shiftDateRepository,
            IEfRepository<Shift> shiftRepository,
            ILogger<UserMonthlyDayShiftQuotaService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _userRepository = userRepository;
            _shiftDateRepository = shiftDateRepository;
            _shiftRepository = shiftRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<UserMonthlyDayShiftQuotaDtoGet>>> GetQuotasAsync(
            UserMonthlyDayShiftQuotaFilter filter)
        {
            var result = await _repository.GetByFilterAsync(filter, "User");
            var items = result.Items.Select(MapToDto).ToList();
            var paged = new PagedResponse<UserMonthlyDayShiftQuotaDtoGet>
            {
                Items = items,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)Math.Max(1, filter.PageSize))
            };
            return ApiResponse<PagedResponse<UserMonthlyDayShiftQuotaDtoGet>>.Success(paged);
        }

        public async Task<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>> GetQuotaAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id, "User");
            if (entity == null)
            {
                return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail("سهمیه صبح/عصر ماهانه یافت نشد.");
            }

            return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Success(MapToDto(entity));
        }

        public async Task<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>> GetQuotaByUserMonthAsync(
            int userId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var error))
            {
                return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail(error);
            }

            var (items, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyDayShiftQuota>(q =>
                    q.UserId == userId &&
                    q.PersianYear == persianYear &&
                    q.PersianMonth == persianMonth),
                "User");

            var entity = items.FirstOrDefault();
            if (entity == null)
            {
                return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail("برای این کاربر در ماه موردنظر سهمیه‌ای ثبت نشده است.");
            }

            return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Success(MapToDto(entity));
        }

        public async Task<ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>> GetDepartmentMonthQuotasAsync(
            int departmentId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var error))
            {
                return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(error);
            }

            var filter = new UserMonthlyDayShiftQuotaFilter
            {
                DepartmentId = departmentId,
                PersianYear = persianYear,
                PersianMonth = persianMonth,
                PageSize = 500
            };
            var result = await _repository.GetByFilterAsync(filter, "User");
            return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Success(result.Items.Select(MapToDto).ToList());
        }

        public async Task<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>> UpsertQuotaAsync(UserMonthlyDayShiftQuotaDtoAdd dto)
        {
            if (!IsValidMonth(dto.PersianYear, dto.PersianMonth, out var monthError))
            {
                return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail(monthError);
            }

            if (!TryNormalize(dto, out var values, out var countError))
            {
                return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail(countError);
            }

            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user == null)
            {
                return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail("کاربر یافت نشد.");
            }

            if (!user.DepartmentId.HasValue)
            {
                return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail("کاربر به دپارتمانی متصل نیست.");
            }

            var permissionError = ValidatePermissions(user, values);
            if (permissionError != null)
            {
                return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail(permissionError);
            }

            if (DayShiftQuotaPermissionValidator.HasAnyConfiguredValue(
                    values.ExactMorningShiftCount, values.MorningFallbackParticipation,
                    values.ExactHolidayMorningShiftCount, values.MorningHolidayFallbackParticipation,
                    values.ExactEveningShiftCount, values.EveningFallbackParticipation,
                    values.ExactHolidayEveningShiftCount, values.EveningHolidayFallbackParticipation))
            {
                var validationError = await ValidateBeforeSaveAsync(
                    user.DepartmentId.Value, dto.PersianYear, dto.PersianMonth, dto.UserId, values);
                if (validationError != null)
                {
                    return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Fail(validationError);
                }
            }

            return await PersistUpsertAsync(dto, user, values);
        }

        public async Task<ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>> UpsertBulkAsync(
            UserMonthlyDayShiftQuotaBulkUpsertDto dto)
        {
            if (!IsValidMonth(dto.PersianYear, dto.PersianMonth, out var monthError))
            {
                return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(monthError);
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail("لیست سهمیه‌ها خالی است.");
            }

            var (deptUsers, _) = await _userRepository.GetByFilterAsync(
                new SimpleFilter<User>(u => u.DepartmentId == dto.DepartmentId && u.IsActive == true));
            var deptUserById = deptUsers.Where(u => u.Id.HasValue).ToDictionary(u => u.Id!.Value);

            var invalidUser = dto.Items.FirstOrDefault(i => !deptUserById.ContainsKey(i.UserId));
            if (invalidUser != null)
            {
                return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(
                    $"کاربر {invalidUser.UserId} در دپارتمان {dto.DepartmentId} یافت نشد.");
            }

            var monthLimits = await TryGetMonthCapacityAsync(dto.DepartmentId, dto.PersianYear, dto.PersianMonth);
            if (monthLimits.Error != null)
            {
                return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(monthLimits.Error);
            }

            var overrides = new Dictionary<int, DayShiftQuotaValues>();
            foreach (var item in dto.Items)
            {
                if (!TryNormalize(item, out var values, out var countError))
                {
                    return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(
                        $"کاربر {item.UserId}: {countError}");
                }

                var permissionError = ValidatePermissions(deptUserById[item.UserId], values);
                if (permissionError != null)
                {
                    return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(
                        $"کاربر {item.UserId}: {permissionError}");
                }

                if (DayShiftQuotaPermissionValidator.HasAnyConfiguredValue(
                        values.ExactMorningShiftCount, values.MorningFallbackParticipation,
                        values.ExactHolidayMorningShiftCount, values.MorningHolidayFallbackParticipation,
                        values.ExactEveningShiftCount, values.EveningFallbackParticipation,
                        values.ExactHolidayEveningShiftCount, values.EveningHolidayFallbackParticipation))
                {
                    var userLimitError = ValidateUserLimits(values, monthLimits, dto.PersianYear, dto.PersianMonth);
                    if (userLimitError != null)
                    {
                        return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(
                            $"کاربر {item.UserId}: {userLimitError}");
                    }
                }

                overrides[item.UserId] = values;
            }

            var capacityError = await ValidateDepartmentCapacityAsync(
                dto.DepartmentId, dto.PersianYear, dto.PersianMonth, overrides, monthLimits);
            if (capacityError != null)
            {
                return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(capacityError);
            }

            var results = new List<UserMonthlyDayShiftQuotaDtoGet>();
            foreach (var item in dto.Items)
            {
                var values = overrides[item.UserId];
                var upsert = await PersistUpsertAsync(
                    new UserMonthlyDayShiftQuotaDtoAdd
                    {
                        UserId = item.UserId,
                        PersianYear = dto.PersianYear,
                        PersianMonth = dto.PersianMonth,
                        ExactMorningShiftCount = values.ExactMorningShiftCount,
                        MorningFallbackParticipation = values.MorningFallbackParticipation,
                        ExactHolidayMorningShiftCount = values.ExactHolidayMorningShiftCount,
                        MorningHolidayFallbackParticipation = values.MorningHolidayFallbackParticipation,
                        ExactEveningShiftCount = values.ExactEveningShiftCount,
                        EveningFallbackParticipation = values.EveningFallbackParticipation,
                        ExactHolidayEveningShiftCount = values.ExactHolidayEveningShiftCount,
                        EveningHolidayFallbackParticipation = values.EveningHolidayFallbackParticipation
                    },
                    deptUserById[item.UserId],
                    values);

                if (!upsert.IsSuccess)
                {
                    return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Fail(
                        $"خطا برای کاربر {item.UserId}: {upsert.Message}");
                }

                if (upsert.Data != null && upsert.Data.Id > 0)
                {
                    results.Add(upsert.Data);
                }
            }

            return ApiResponse<List<UserMonthlyDayShiftQuotaDtoGet>>.Success(
                results, "سهمیه‌های صبح/عصر ماهانه ذخیره شدند.");
        }

        public async Task<ApiResponse<string>> DeleteQuotaAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                return ApiResponse<string>.Fail("سهمیه صبح/عصر ماهانه یافت نشد.");
            }

            _repository.Delete(entity);
            await _repository.SaveAsync();
            return ApiResponse<string>.Success("حذف شد.", "سهمیه صبح/عصر ماهانه حذف شد.");
        }

        private async Task<string?> ValidateBeforeSaveAsync(
            int departmentId,
            int persianYear,
            int persianMonth,
            int userId,
            DayShiftQuotaValues values)
        {
            var monthLimits = await TryGetMonthCapacityAsync(departmentId, persianYear, persianMonth);
            if (monthLimits.Error != null)
            {
                return monthLimits.Error;
            }

            var userLimitError = ValidateUserLimits(values, monthLimits, persianYear, persianMonth);
            if (userLimitError != null)
            {
                return userLimitError;
            }

            return await ValidateDepartmentCapacityAsync(
                departmentId, persianYear, persianMonth,
                new Dictionary<int, DayShiftQuotaValues> { [userId] = values },
                monthLimits);
        }

        private async Task<string?> ValidateDepartmentCapacityAsync(
            int departmentId,
            int persianYear,
            int persianMonth,
            IReadOnlyDictionary<int, DayShiftQuotaValues> overrides,
            MonthCapacity capacity)
        {
            var (deptUsers, _) = await _userRepository.GetByFilterAsync(
                new SimpleFilter<User>(u => u.DepartmentId == departmentId && u.IsActive == true));
            var deptUserIds = deptUsers.Where(u => u.Id.HasValue).Select(u => u.Id!.Value).ToHashSet();
            if (deptUserIds.Count == 0)
            {
                return "کاربر فعالی در این دپارتمان یافت نشد.";
            }

            var (existingQuotas, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyDayShiftQuota>(q =>
                    q.PersianYear == persianYear &&
                    q.PersianMonth == persianMonth &&
                    deptUserIds.Contains(q.UserId)));

            var projected = existingQuotas.ToDictionary(q => q.UserId, q => DayShiftQuotaValues.FromEntity(q));
            foreach (var (userId, values) in overrides)
            {
                if (!deptUserIds.Contains(userId))
                {
                    continue;
                }

                if (DayShiftQuotaPermissionValidator.HasAnyConfiguredValue(
                        values.ExactMorningShiftCount, values.MorningFallbackParticipation,
                        values.ExactHolidayMorningShiftCount, values.MorningHolidayFallbackParticipation,
                        values.ExactEveningShiftCount, values.EveningFallbackParticipation,
                        values.ExactHolidayEveningShiftCount, values.EveningHolidayFallbackParticipation))
                {
                    projected[userId] = values;
                }
                else
                {
                    projected.Remove(userId);
                }
            }

            var totalMorning = projected.Values.Sum(v => v.ExactMorningShiftCount ?? 0);
            var totalMorningHoliday = projected.Values.Sum(v => v.ExactHolidayMorningShiftCount ?? 0);
            var totalEvening = projected.Values.Sum(v => v.ExactEveningShiftCount ?? 0);
            var totalEveningHoliday = projected.Values.Sum(v => v.ExactHolidayEveningShiftCount ?? 0);

            if (totalMorning > capacity.MorningTotalSlots)
            {
                return
                    $"مجموع سهمیه شیفت صبح ({totalMorning}) از ظرفیت ماه {persianYear}/{persianMonth:00} " +
                    $"({capacity.MorningTotalSlots} = {capacity.DaysInMonth} روز × {capacity.MorningHeadcountPerDay} نفر) بیشتر است.";
            }

            if (totalMorningHoliday > capacity.MorningHolidaySlots)
            {
                return
                    $"مجموع سهمیه صبح تعطیل ({totalMorningHoliday}) از ظرفیت روزهای تعطیل ماه " +
                    $"{persianYear}/{persianMonth:00} ({capacity.MorningHolidaySlots}) بیشتر است.";
            }

            if (totalEvening > capacity.EveningTotalSlots)
            {
                return
                    $"مجموع سهمیه شیفت عصر ({totalEvening}) از ظرفیت ماه {persianYear}/{persianMonth:00} " +
                    $"({capacity.EveningTotalSlots} = {capacity.DaysInMonth} روز × {capacity.EveningHeadcountPerDay} نفر) بیشتر است.";
            }

            if (totalEveningHoliday > capacity.EveningHolidaySlots)
            {
                return
                    $"مجموع سهمیه عصر تعطیل ({totalEveningHoliday}) از ظرفیت روزهای تعطیل ماه " +
                    $"{persianYear}/{persianMonth:00} ({capacity.EveningHolidaySlots}) بیشتر است.";
            }

            return null;
        }

        private static string? ValidateUserLimits(
            DayShiftQuotaValues values,
            MonthCapacity capacity,
            int persianYear,
            int persianMonth)
        {
            var morningError = UserMonthlyDayShiftQuotaLimits.ValidateMorning(
                values.ExactMorningShiftCount,
                values.ExactHolidayMorningShiftCount,
                capacity.DaysInMonth,
                capacity.HolidayDaysInMonth,
                persianYear,
                persianMonth);
            if (morningError != null)
            {
                return morningError;
            }

            return UserMonthlyDayShiftQuotaLimits.ValidateEvening(
                values.ExactEveningShiftCount,
                values.ExactHolidayEveningShiftCount,
                capacity.DaysInMonth,
                capacity.HolidayDaysInMonth,
                persianYear,
                persianMonth);
        }

        private static string? ValidatePermissions(User user, DayShiftQuotaValues values)
        {
            var permissions = ShiftEligibilityResolver.ResolvePermissions(
                user.AllowedShiftPermissions,
                user.ShiftType ?? ShiftTypes.FixedShift,
                user.ShiftSubType ?? ShiftSubTypes.FixedMorning,
                user.TwoShiftRotationPattern);

            return DayShiftQuotaPermissionValidator.Validate(
                user,
                permissions,
                values.ExactMorningShiftCount,
                values.MorningFallbackParticipation,
                values.ExactHolidayMorningShiftCount,
                values.MorningHolidayFallbackParticipation,
                values.ExactEveningShiftCount,
                values.EveningFallbackParticipation,
                values.ExactHolidayEveningShiftCount,
                values.EveningHolidayFallbackParticipation);
        }

        private async Task<ApiResponse<UserMonthlyDayShiftQuotaDtoGet>> PersistUpsertAsync(
            UserMonthlyDayShiftQuotaDtoAdd dto,
            User user,
            DayShiftQuotaValues values)
        {
            var (existing, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyDayShiftQuota>(q =>
                    q.UserId == dto.UserId &&
                    q.PersianYear == dto.PersianYear &&
                    q.PersianMonth == dto.PersianMonth));

            var entity = existing.FirstOrDefault();
            var now = DateTime.Now;
            var actorId = GetActorUserId();
            var hasValues = DayShiftQuotaPermissionValidator.HasAnyConfiguredValue(
                values.ExactMorningShiftCount, values.MorningFallbackParticipation,
                values.ExactHolidayMorningShiftCount, values.MorningHolidayFallbackParticipation,
                values.ExactEveningShiftCount, values.EveningFallbackParticipation,
                values.ExactHolidayEveningShiftCount, values.EveningHolidayFallbackParticipation);

            if (entity == null)
            {
                if (!hasValues)
                {
                    return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Success(
                        new UserMonthlyDayShiftQuotaDtoGet
                        {
                            UserId = dto.UserId,
                            PersianYear = dto.PersianYear,
                            PersianMonth = dto.PersianMonth
                        },
                        "سهمیه صبح/عصر برای این کاربر تنظیم نشده است.");
                }

                entity = new UserMonthlyDayShiftQuota
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
                if (!hasValues)
                {
                    _repository.Delete(entity);
                    await _repository.SaveAsync();
                    return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Success(
                        new UserMonthlyDayShiftQuotaDtoGet
                        {
                            UserId = dto.UserId,
                            PersianYear = dto.PersianYear,
                            PersianMonth = dto.PersianMonth
                        },
                        "سهمیه صبح/عصر ماهانه حذف شد.");
                }

                ApplyValues(entity, values);
                entity.UpdateDate = now;
                entity.TheUserId = actorId;
                _repository.Update(entity);
            }

            await _repository.SaveAsync();
            entity.User = user;
            return ApiResponse<UserMonthlyDayShiftQuotaDtoGet>.Success(MapToDto(entity), "سهمیه صبح/عصر ماهانه ذخیره شد.");
        }

        private async Task<MonthCapacity> TryGetMonthCapacityAsync(
            int departmentId,
            int persianYear,
            int persianMonth)
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
                return MonthCapacity.Failed(
                    $"در ShiftDates برای ماه شمسی {persianYear}/{persianMonth:00} هیچ روزی ثبت نشده است.");
            }

            if (monthDates.Count < daysInMonth)
            {
                return MonthCapacity.Failed(
                    $"تقویم ShiftDates برای ماه {persianYear}/{persianMonth:00} ناقص است " +
                    $"({monthDates.Count} از {daysInMonth} روز).");
            }

            var holidayDates = shiftDates
                .Where(d => d.IsHoliday == true && d.Date.HasValue)
                .Select(d => d.Date!.Value.Date)
                .ToHashSet();

            var (days, holidayDays) = PersianMonthDayCalendar.CountDayCapacities(monthDates, holidayDates);

            var (departmentShifts, _) = await _shiftRepository.GetByFilterAsync(
                new SimpleFilter<Shift>(s => s.DepartmentId == departmentId),
                "RequiredSpecialties");

            var (morningTotal, morningHoliday, morningHeadcount, _) =
                DepartmentDayShiftQuotaCapacityCalculator.Calculate(
                    ShiftLabel.Morning, departmentShifts, monthDates, holidayDates);
            var (eveningTotal, eveningHoliday, eveningHeadcount, _) =
                DepartmentDayShiftQuotaCapacityCalculator.Calculate(
                    ShiftLabel.Evening, departmentShifts, monthDates, holidayDates);

            return new MonthCapacity(
                days,
                holidayDays,
                morningTotal,
                morningHoliday,
                morningHeadcount,
                eveningTotal,
                eveningHoliday,
                eveningHeadcount,
                null);
        }

        private static void ApplyValues(UserMonthlyDayShiftQuota entity, DayShiftQuotaValues values)
        {
            entity.ExactMorningShiftCount = values.ExactMorningShiftCount;
            entity.MorningFallbackParticipation = values.MorningFallbackParticipation;
            entity.ExactHolidayMorningShiftCount = values.ExactHolidayMorningShiftCount;
            entity.MorningHolidayFallbackParticipation = values.MorningHolidayFallbackParticipation;
            entity.ExactEveningShiftCount = values.ExactEveningShiftCount;
            entity.EveningFallbackParticipation = values.EveningFallbackParticipation;
            entity.ExactHolidayEveningShiftCount = values.ExactHolidayEveningShiftCount;
            entity.EveningHolidayFallbackParticipation = values.EveningHolidayFallbackParticipation;
        }

        private static UserMonthlyDayShiftQuotaDtoGet MapToDto(UserMonthlyDayShiftQuota entity) => new()
        {
            Id = entity.Id ?? 0,
            UserId = entity.UserId,
            UserName = entity.User?.FullName,
            DepartmentId = entity.User?.DepartmentId,
            PersianYear = entity.PersianYear,
            PersianMonth = entity.PersianMonth,
            ExactMorningShiftCount = entity.ExactMorningShiftCount,
            MorningFallbackParticipation = entity.MorningFallbackParticipation,
            ExactHolidayMorningShiftCount = entity.ExactHolidayMorningShiftCount,
            MorningHolidayFallbackParticipation = entity.MorningHolidayFallbackParticipation,
            ExactEveningShiftCount = entity.ExactEveningShiftCount,
            EveningFallbackParticipation = entity.EveningFallbackParticipation,
            ExactHolidayEveningShiftCount = entity.ExactHolidayEveningShiftCount,
            EveningHolidayFallbackParticipation = entity.EveningHolidayFallbackParticipation
        };

        private static bool IsValidMonth(int year, int month, out string error)
        {
            if (year < 1300 || year > 1500)
            {
                error = "سال شمسی نامعتبر است.";
                return false;
            }

            if (month < 1 || month > 12)
            {
                error = "ماه شمسی باید بین ۱ تا ۱۲ باشد.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryNormalize(UserMonthlyDayShiftQuotaDtoAdd dto, out DayShiftQuotaValues values, out string error) =>
            TryNormalizeCore(
                dto.ExactMorningShiftCount, dto.MorningFallbackParticipation,
                dto.ExactHolidayMorningShiftCount, dto.MorningHolidayFallbackParticipation,
                dto.ExactEveningShiftCount, dto.EveningFallbackParticipation,
                dto.ExactHolidayEveningShiftCount, dto.EveningHolidayFallbackParticipation,
                out values, out error);

        private static bool TryNormalize(UserMonthlyDayShiftQuotaItemDto dto, out DayShiftQuotaValues values, out string error) =>
            TryNormalizeCore(
                dto.ExactMorningShiftCount, dto.MorningFallbackParticipation,
                dto.ExactHolidayMorningShiftCount, dto.MorningHolidayFallbackParticipation,
                dto.ExactEveningShiftCount, dto.EveningFallbackParticipation,
                dto.ExactHolidayEveningShiftCount, dto.EveningHolidayFallbackParticipation,
                out values, out error);

        private static bool TryNormalizeCore(
            int? morning,
            bool? morningFallback,
            int? holidayMorning,
            bool? holidayMorningFallback,
            int? evening,
            bool? eveningFallback,
            int? holidayEvening,
            bool? holidayEveningFallback,
            out DayShiftQuotaValues values,
            out string error)
        {
            values = new DayShiftQuotaValues(
                morning, morningFallback, holidayMorning, holidayMorningFallback,
                evening, eveningFallback, holidayEvening, holidayEveningFallback);
            error = string.Empty;

            if (morning.HasValue && morning.Value < 0)
            {
                error = "تعداد شیفت صبح نمی‌تواند منفی باشد.";
                return false;
            }

            if (evening.HasValue && evening.Value < 0)
            {
                error = "تعداد شیفت عصر نمی‌تواند منفی باشد.";
                return false;
            }

            if (holidayMorning.HasValue && holidayMorning.Value < 0)
            {
                error = "تعداد شیفت صبح تعطیل نمی‌تواند منفی باشد.";
                return false;
            }

            if (holidayEvening.HasValue && holidayEvening.Value < 0)
            {
                error = "تعداد شیفت عصر تعطیل نمی‌تواند منفی باشد.";
                return false;
            }

            if (morning.HasValue && holidayMorning.HasValue && holidayMorning.Value > morning.Value)
            {
                error = "تعداد شیفت صبح تعطیل نمی‌تواند بیشتر از تعداد کل شیفت صبح باشد.";
                return false;
            }

            if (evening.HasValue && holidayEvening.HasValue && holidayEvening.Value > evening.Value)
            {
                error = "تعداد شیفت عصر تعطیل نمی‌تواند بیشتر از تعداد کل شیفت عصر باشد.";
                return false;
            }

            return true;
        }

        private int? GetActorUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private readonly record struct DayShiftQuotaValues(
            int? ExactMorningShiftCount,
            bool? MorningFallbackParticipation,
            int? ExactHolidayMorningShiftCount,
            bool? MorningHolidayFallbackParticipation,
            int? ExactEveningShiftCount,
            bool? EveningFallbackParticipation,
            int? ExactHolidayEveningShiftCount,
            bool? EveningHolidayFallbackParticipation)
        {
            public static DayShiftQuotaValues FromEntity(UserMonthlyDayShiftQuota entity) => new(
                entity.ExactMorningShiftCount,
                entity.MorningFallbackParticipation,
                entity.ExactHolidayMorningShiftCount,
                entity.MorningHolidayFallbackParticipation,
                entity.ExactEveningShiftCount,
                entity.EveningFallbackParticipation,
                entity.ExactHolidayEveningShiftCount,
                entity.EveningHolidayFallbackParticipation);
        }

        private readonly record struct MonthCapacity(
            int DaysInMonth,
            int HolidayDaysInMonth,
            int MorningTotalSlots,
            int MorningHolidaySlots,
            int MorningHeadcountPerDay,
            int EveningTotalSlots,
            int EveningHolidaySlots,
            int EveningHeadcountPerDay,
            string? Error)
        {
            public static MonthCapacity Failed(string message) =>
                new(0, 0, 0, 0, 0, 0, 0, 0, message);
        }
    }
}
