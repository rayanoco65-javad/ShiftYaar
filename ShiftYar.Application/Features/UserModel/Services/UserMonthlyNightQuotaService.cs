using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.UserModel;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using ShiftYar.Domain.Enums.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.UserModel.Services
{
    public class UserMonthlyNightQuotaService : IUserMonthlyNightQuotaService
    {
        private readonly IEfRepository<UserMonthlyNightQuota> _repository;
        private readonly IEfRepository<User> _userRepository;
        private readonly IEfRepository<ShiftDate> _shiftDateRepository;
        private readonly IEfRepository<Shift> _shiftRepository;
        private readonly IEfRepository<ShiftRequest> _shiftRequestRepository;
        private readonly ILogger<UserMonthlyNightQuotaService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserMonthlyNightQuotaService(
            IEfRepository<UserMonthlyNightQuota> repository,
            IEfRepository<User> userRepository,
            IEfRepository<ShiftDate> shiftDateRepository,
            IEfRepository<Shift> shiftRepository,
            IEfRepository<ShiftRequest> shiftRequestRepository,
            ILogger<UserMonthlyNightQuotaService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _userRepository = userRepository;
            _shiftDateRepository = shiftDateRepository;
            _shiftRepository = shiftRepository;
            _shiftRequestRepository = shiftRequestRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<UserMonthlyNightQuotaDtoGet>>> GetQuotasAsync(UserMonthlyNightQuotaFilter filter)
        {
            var result = await _repository.GetByFilterAsync(filter, "User");
            var items = result.Items.Select(MapToDto).ToList();
            var paged = new PagedResponse<UserMonthlyNightQuotaDtoGet>
            {
                Items = items,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)Math.Max(1, filter.PageSize))
            };
            return ApiResponse<PagedResponse<UserMonthlyNightQuotaDtoGet>>.Success(paged);
        }

        public async Task<ApiResponse<UserMonthlyNightQuotaDtoGet>> GetQuotaAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id, "User");
            if (entity == null)
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail("سهمیه شب ماهانه یافت نشد.");
            }

            return ApiResponse<UserMonthlyNightQuotaDtoGet>.Success(MapToDto(entity));
        }

        public async Task<ApiResponse<UserMonthlyNightQuotaDtoGet>> GetQuotaByUserMonthAsync(int userId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var error))
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(error);
            }

            var (items, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyNightQuota>(q =>
                    q.UserId == userId &&
                    q.PersianYear == persianYear &&
                    q.PersianMonth == persianMonth),
                "User");

            var entity = items.FirstOrDefault();
            if (entity == null)
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail("برای این کاربر در ماه موردنظر سهمیه‌ای ثبت نشده است.");
            }

            return ApiResponse<UserMonthlyNightQuotaDtoGet>.Success(MapToDto(entity));
        }

        public async Task<ApiResponse<List<UserMonthlyNightQuotaDtoGet>>> GetDepartmentMonthQuotasAsync(
            int departmentId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var error))
            {
                return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(error);
            }

            var filter = new UserMonthlyNightQuotaFilter
            {
                DepartmentId = departmentId,
                PersianYear = persianYear,
                PersianMonth = persianMonth,
                PageSize = 500
            };
            var result = await _repository.GetByFilterAsync(filter, "User");
            return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Success(result.Items.Select(MapToDto).ToList());
        }

        public async Task<ApiResponse<UserMonthlyNightQuotaDtoGet>> UpsertQuotaAsync(UserMonthlyNightQuotaDtoAdd dto)
        {
            if (!IsValidMonth(dto.PersianYear, dto.PersianMonth, out var monthError))
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(monthError);
            }

            if (!TryNormalize(dto, out var values, out var countError))
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(countError);
            }

            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user == null)
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail("کاربر یافت نشد.");
            }

            if (!user.DepartmentId.HasValue)
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail("کاربر به دپارتمانی متصل نیست.");
            }

            var permissions = ShiftEligibilityResolver.ResolvePermissions(
                user.AllowedShiftPermissions,
                user.ShiftType ?? ShiftTypes.FixedShift,
                user.ShiftSubType ?? ShiftSubTypes.FixedMorning,
                user.TwoShiftRotationPattern);
            var permissionError = NightQuotaPermissionValidator.Validate(
                user,
                permissions,
                values.ExactNightShiftCount,
                values.NightFallbackParticipation,
                values.ExactHolidayWeekendNightShiftCount,
                values.HolidayWeekendNightFallbackParticipation);
            if (permissionError != null)
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(permissionError);
            }

            if (NightQuotaPermissionValidator.HasAnyConfiguredValue(
                    values.ExactNightShiftCount,
                    values.NightFallbackParticipation,
                    values.ExactHolidayWeekendNightShiftCount,
                    values.HolidayWeekendNightFallbackParticipation))
            {
                if (values.ExactNightShiftCount.HasValue || values.ExactHolidayWeekendNightShiftCount.HasValue)
                {
                    var monthLimits = await TryGetMonthNightCapacityAsync(
                        user.DepartmentId.Value, dto.PersianYear, dto.PersianMonth);
                    if (monthLimits.Error != null)
                    {
                        return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(monthLimits.Error);
                    }

                    var userLimitError = UserMonthlyNightQuotaLimits.Validate(
                        values.ExactNightShiftCount,
                        values.ExactHolidayWeekendNightShiftCount,
                        monthLimits.NightDays,
                        monthLimits.HolidayWeekendNightDays,
                        dto.PersianYear,
                        dto.PersianMonth);
                    if (userLimitError != null)
                    {
                        return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(userLimitError);
                    }

                    var capacityError = await ValidateDepartmentMonthCapacityAsync(
                        user.DepartmentId.Value,
                        dto.PersianYear,
                        dto.PersianMonth,
                        overrides: new Dictionary<int, (int? Night, int? Holiday)>
                        {
                            [dto.UserId] = (values.ExactNightShiftCount, values.ExactHolidayWeekendNightShiftCount)
                        });
                    if (capacityError != null)
                    {
                        return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(capacityError);
                    }
                }
            }

            return await PersistUpsertAsync(dto, user, values);
        }

        public async Task<ApiResponse<List<UserMonthlyNightQuotaDtoGet>>> UpsertBulkAsync(UserMonthlyNightQuotaBulkUpsertDto dto)
        {
            if (!IsValidMonth(dto.PersianYear, dto.PersianMonth, out var monthError))
            {
                return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(monthError);
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail("لیست سهمیه‌ها خالی است.");
            }

            var (deptUsers, _) = await _userRepository.GetByFilterAsync(
                new SimpleFilter<User>(u => u.DepartmentId == dto.DepartmentId && u.IsActive == true));
            var deptUserById = deptUsers
                .Where(u => u.Id.HasValue)
                .ToDictionary(u => u.Id!.Value);

            var invalidUser = dto.Items.FirstOrDefault(i => !deptUserById.ContainsKey(i.UserId));
            if (invalidUser != null)
            {
                return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(
                    $"کاربر {invalidUser.UserId} در دپارتمان {dto.DepartmentId} یافت نشد.");
            }

            var monthLimits = await TryGetMonthNightCapacityAsync(dto.DepartmentId, dto.PersianYear, dto.PersianMonth);
            if (monthLimits.Error != null)
            {
                return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(monthLimits.Error);
            }

            var overrides = new Dictionary<int, NightQuotaValues>();
            foreach (var item in dto.Items)
            {
                if (!TryNormalize(item, out var values, out var countError))
                {
                    return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(
                        $"کاربر {item.UserId}: {countError}");
                }

                var permissionError = NightQuotaPermissionValidator.Validate(
                    deptUserById[item.UserId],
                    ShiftEligibilityResolver.ResolvePermissions(
                        deptUserById[item.UserId].AllowedShiftPermissions,
                        deptUserById[item.UserId].ShiftType ?? ShiftTypes.FixedShift,
                        deptUserById[item.UserId].ShiftSubType ?? ShiftSubTypes.FixedMorning,
                        deptUserById[item.UserId].TwoShiftRotationPattern),
                    values.ExactNightShiftCount,
                    values.NightFallbackParticipation,
                    values.ExactHolidayWeekendNightShiftCount,
                    values.HolidayWeekendNightFallbackParticipation);
                if (permissionError != null)
                {
                    return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(
                        $"کاربر {item.UserId}: {permissionError}");
                }

                if (values.ExactNightShiftCount.HasValue || values.ExactHolidayWeekendNightShiftCount.HasValue)
                {
                    var userLimitError = UserMonthlyNightQuotaLimits.Validate(
                        values.ExactNightShiftCount,
                        values.ExactHolidayWeekendNightShiftCount,
                        monthLimits.NightDays,
                        monthLimits.HolidayWeekendNightDays,
                        dto.PersianYear,
                        dto.PersianMonth);
                    if (userLimitError != null)
                    {
                        return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(
                            $"کاربر {item.UserId}: {userLimitError}");
                    }
                }

                overrides[item.UserId] = values;
            }

            var capacityOverrides = overrides
                .Where(x => x.Value.ExactNightShiftCount.HasValue || x.Value.ExactHolidayWeekendNightShiftCount.HasValue)
                .ToDictionary(x => x.Key, x => (x.Value.ExactNightShiftCount, x.Value.ExactHolidayWeekendNightShiftCount));
            var capacityError = await ValidateDepartmentMonthCapacityAsync(
                dto.DepartmentId,
                dto.PersianYear,
                dto.PersianMonth,
                capacityOverrides);
            if (capacityError != null)
            {
                return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(capacityError);
            }

            var results = new List<UserMonthlyNightQuotaDtoGet>();
            foreach (var item in dto.Items)
            {
                var values = overrides[item.UserId];
                var upsert = await PersistUpsertAsync(
                    new UserMonthlyNightQuotaDtoAdd
                    {
                        UserId = item.UserId,
                        PersianYear = dto.PersianYear,
                        PersianMonth = dto.PersianMonth,
                        ExactNightShiftCount = values.ExactNightShiftCount,
                        ExactHolidayWeekendNightShiftCount = values.ExactHolidayWeekendNightShiftCount,
                        NightFallbackParticipation = values.NightFallbackParticipation,
                        HolidayWeekendNightFallbackParticipation = values.HolidayWeekendNightFallbackParticipation
                    },
                    deptUserById[item.UserId],
                    values);

                if (!upsert.IsSuccess)
                {
                    return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(
                        $"خطا برای کاربر {item.UserId}: {upsert.Message}");
                }

                if (upsert.Data != null && upsert.Data.Id > 0)
                {
                    results.Add(upsert.Data);
                }
            }

            return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Success(results, "سهمیه‌های شب ماهانه ذخیره شدند.");
        }

        public async Task<ApiResponse<string>> DeleteQuotaAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                return ApiResponse<string>.Fail("سهمیه شب ماهانه یافت نشد.");
            }

            var approvedFloorError = await ValidateQuotaAgainstApprovedNightRequestsAsync(
                entity.UserId, entity.PersianYear, entity.PersianMonth, nightQuota: null, holidayQuota: null);
            if (approvedFloorError != null)
            {
                return ApiResponse<string>.Fail(approvedFloorError);
            }

            _repository.Delete(entity);
            await _repository.SaveAsync();
            return ApiResponse<string>.Success("حذف شد.", "سهمیه شب ماهانه حذف شد.");
        }

        private async Task<ApiResponse<UserMonthlyNightQuotaDtoGet>> PersistUpsertAsync(
            UserMonthlyNightQuotaDtoAdd dto,
            User user,
            NightQuotaValues values)
        {
            var approvedFloorError = await ValidateQuotaAgainstApprovedNightRequestsAsync(
                dto.UserId,
                dto.PersianYear,
                dto.PersianMonth,
                values.ExactNightShiftCount,
                values.ExactHolidayWeekendNightShiftCount);
            if (approvedFloorError != null)
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(approvedFloorError);
            }

            var (existing, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyNightQuota>(q =>
                    q.UserId == dto.UserId &&
                    q.PersianYear == dto.PersianYear &&
                    q.PersianMonth == dto.PersianMonth));

            var entity = existing.FirstOrDefault();
            var now = DateTime.Now;
            var actorId = GetActorUserId();
            var hasAny = NightQuotaPermissionValidator.HasAnyConfiguredValue(
                values.ExactNightShiftCount,
                values.NightFallbackParticipation,
                values.ExactHolidayWeekendNightShiftCount,
                values.HolidayWeekendNightFallbackParticipation);

            if (entity == null)
            {
                if (!hasAny)
                {
                    return ApiResponse<UserMonthlyNightQuotaDtoGet>.Success(
                        new UserMonthlyNightQuotaDtoGet
                        {
                            UserId = dto.UserId,
                            PersianYear = dto.PersianYear,
                            PersianMonth = dto.PersianMonth
                        },
                        "سهمیه شب برای این کاربر تنظیم نشده است.");
                }

                entity = new UserMonthlyNightQuota
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
                    return ApiResponse<UserMonthlyNightQuotaDtoGet>.Success(
                        new UserMonthlyNightQuotaDtoGet
                        {
                            UserId = dto.UserId,
                            PersianYear = dto.PersianYear,
                            PersianMonth = dto.PersianMonth
                        },
                        "سهمیه شب ماهانه حذف شد.");
                }

                ApplyValues(entity, values);
                entity.UpdateDate = now;
                entity.TheUserId = actorId;
                _repository.Update(entity);
            }

            await _repository.SaveAsync();
            entity.User = user;
            _logger.LogInformation(
                "Upserted monthly night quota UserId={UserId} {Year}/{Month} Night={Night} Holiday={Holiday} NightFb={NightFb} HolFb={HolFb}",
                dto.UserId,
                dto.PersianYear,
                dto.PersianMonth,
                values.ExactNightShiftCount,
                values.ExactHolidayWeekendNightShiftCount,
                values.NightFallbackParticipation,
                values.HolidayWeekendNightFallbackParticipation);

            return ApiResponse<UserMonthlyNightQuotaDtoGet>.Success(MapToDto(entity), "سهمیه شب ماهانه ذخیره شد.");
        }

        private static void ApplyValues(UserMonthlyNightQuota entity, NightQuotaValues values)
        {
            entity.ExactNightShiftCount = values.ExactNightShiftCount;
            entity.ExactHolidayWeekendNightShiftCount = values.ExactHolidayWeekendNightShiftCount;
            entity.NightFallbackParticipation = values.NightFallbackParticipation;
            entity.HolidayWeekendNightFallbackParticipation = values.HolidayWeekendNightFallbackParticipation;
        }

        /// <summary>
        /// مجموع سهمیه‌های دپارتمان در ماه (با اعمال overrides) نباید از ظرفیت تقویم بیشتر باشد.
        /// </summary>
        private async Task<string?> ValidateDepartmentMonthCapacityAsync(
            int departmentId,
            int persianYear,
            int persianMonth,
            IReadOnlyDictionary<int, (int? Night, int? Holiday)> overrides)
        {
            var capacity = await TryGetMonthNightCapacityAsync(departmentId, persianYear, persianMonth);
            if (capacity.Error != null)
            {
                return capacity.Error;
            }

            var (deptUsers, _) = await _userRepository.GetByFilterAsync(
                new SimpleFilter<User>(u => u.DepartmentId == departmentId && u.IsActive == true));
            var deptUserIds = deptUsers.Where(u => u.Id.HasValue).Select(u => u.Id!.Value).ToHashSet();
            if (deptUserIds.Count == 0)
            {
                return "کاربر فعالی در این دپارتمان یافت نشد.";
            }

            var (existingQuotas, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyNightQuota>(q =>
                    q.PersianYear == persianYear &&
                    q.PersianMonth == persianMonth &&
                    deptUserIds.Contains(q.UserId)));

            var projectedNight = new Dictionary<int, int>();
            var projectedHoliday = new Dictionary<int, int>();

            foreach (var q in existingQuotas)
            {
                if (q.ExactNightShiftCount.HasValue)
                {
                    projectedNight[q.UserId] = q.ExactNightShiftCount.Value;
                }

                if (q.ExactHolidayWeekendNightShiftCount.HasValue)
                {
                    projectedHoliday[q.UserId] = q.ExactHolidayWeekendNightShiftCount.Value;
                }
            }

            foreach (var (userId, values) in overrides)
            {
                if (!deptUserIds.Contains(userId))
                {
                    continue;
                }

                if (values.Night.HasValue)
                {
                    projectedNight[userId] = values.Night.Value;
                }
                else
                {
                    projectedNight.Remove(userId);
                }

                if (values.Holiday.HasValue)
                {
                    projectedHoliday[userId] = values.Holiday.Value;
                }
                else
                {
                    projectedHoliday.Remove(userId);
                }
            }

            var totalNightQuota = projectedNight.Values.Sum();
            var totalHolidayQuota = projectedHoliday.Values.Sum();

            if (totalNightQuota > capacity.TotalNightSlots)
            {
                return
                    $"مجموع سهمیه شیفت شب کاربران ({totalNightQuota}) از ظرفیت ماه {persianYear}/{persianMonth:00} " +
                    $"({capacity.TotalNightSlots} شیفت شب = {capacity.NightDays} شب × {capacity.HeadcountPerRegularNight} نفر " +
                    $"بر اساس نیازمندی تخصص شیفت شب) بیشتر است. لطفاً مقادیر را کاهش دهید.";
            }

            if (totalHolidayQuota > capacity.HolidayWeekendNightSlots)
            {
                return
                    $"مجموع سهمیه شب تعطیل/آخرهفته کاربران ({totalHolidayQuota}) از ظرفیت شب‌های تعطیل و آخر هفته " +
                    $"ماه {persianYear}/{persianMonth:00} ({capacity.HolidayWeekendNightSlots} شیفت شب = " +
                    $"{capacity.HolidayWeekendNightDays} شب × {capacity.HeadcountPerHolidayNight} نفر " +
                    $"بر اساس نیازمندی تخصص شیفت شب) بیشتر است. لطفاً مقادیر را کاهش دهید.";
            }

            return null;
        }

        private async Task<string?> ValidateQuotaAgainstApprovedNightRequestsAsync(
            int userId,
            int persianYear,
            int persianMonth,
            int? nightQuota,
            int? holidayQuota)
        {
            var (monthStart, monthEnd, _) = PersianMonthNightCalendar.GetMonthBounds(persianYear, persianMonth);
            var (approvedRequests, _) = await _shiftRequestRepository.GetByFilterAsync(
                new SimpleFilter<ShiftRequest>(r =>
                    r.UserId == userId &&
                    r.Status == RequestStatus.Approved &&
                    r.RequestAction == RequestAction.RequestToBeOnShift &&
                    r.RequestType == RequestType.SpecificShift &&
                    r.ShiftLabel == ShiftLabel.Night &&
                    r.RequestDate != null &&
                    r.RequestDate >= monthStart &&
                    r.RequestDate <= monthEnd));

            var approvedNightCount = NightQuotaRequestLinker.CountApprovedNightOnRequestsInMonth(
                approvedRequests, userId, persianYear, persianMonth);

            // حذف کامل سهمیه در حالی که درخواست شب تأییدشده هست ممنوع است
            if (!nightQuota.HasValue && !holidayQuota.HasValue)
            {
                if (approvedNightCount > 0)
                {
                    return NightQuotaRequestLinker.ValidateQuotaAgainstApprovedNightRequests(
                        null, approvedNightCount, null, 0, approvedNightCount, persianYear, persianMonth);
                }

                return null;
            }

            var (shiftDates, _) = await _shiftDateRepository.GetByFilterAsync(
                new SimpleFilter<ShiftDate>(d =>
                    d.Date != null &&
                    d.Date >= monthStart &&
                    d.Date <= monthEnd.AddDays(1)));
            var holidays = shiftDates
                .Where(d => d.IsHoliday == true && d.Date.HasValue)
                .Select(d => d.Date!.Value.Date)
                .ToHashSet();

            bool IsHolidayNight(DateTime date) =>
                HolidayWeekendNightRules.IsHolidayWeekendNight(date, holidays);

            var approvedHolidayCount = NightQuotaRequestLinker.CountApprovedHolidayNightOnRequestsInMonth(
                approvedRequests, userId, persianYear, persianMonth, IsHolidayNight);

            var approvedNonHolidayCount = NightQuotaRequestLinker.CountApprovedNonHolidayNightOnRequestsInMonth(
                approvedRequests, userId, persianYear, persianMonth, IsHolidayNight);

            return NightQuotaRequestLinker.ValidateQuotaAgainstApprovedNightRequests(
                nightQuota,
                approvedNightCount,
                holidayQuota,
                approvedHolidayCount,
                approvedNonHolidayCount,
                persianYear,
                persianMonth);
        }

        private async Task<(
            int TotalNightSlots,
            int HolidayWeekendNightSlots,
            int NightDays,
            int HolidayWeekendNightDays,
            int HeadcountPerRegularNight,
            int HeadcountPerHolidayNight,
            string? Error)> TryGetMonthNightCapacityAsync(
            int departmentId,
            int persianYear,
            int persianMonth)
        {
            var (monthStart, monthEnd, daysInMonth) = PersianMonthNightCalendar.GetMonthBounds(persianYear, persianMonth);
            // یک روز بعد برای تشخیص «شب قبل از تعطیل» در مرز ماه
            var loadEnd = monthEnd.AddDays(1);

            var (shiftDates, _) = await _shiftDateRepository.GetByFilterAsync(
                new SimpleFilter<ShiftDate>(d =>
                    d.Date != null &&
                    d.Date >= monthStart &&
                    d.Date <= loadEnd));

            var monthDates = shiftDates
                .Where(d => d.Date.HasValue && d.Date.Value.Date >= monthStart && d.Date.Value.Date <= monthEnd)
                .Select(d => d.Date!.Value.Date)
                .Distinct()
                .ToList();

            if (monthDates.Count == 0)
            {
                return (0, 0, 0, 0, 0, 0,
                    $"در ShiftDates برای ماه شمسی {persianYear}/{persianMonth:00} هیچ روزی ثبت نشده است. " +
                    "ابتدا تقویم را تکمیل کنید.");
            }

            if (monthDates.Count < daysInMonth)
            {
                return (0, 0, 0, 0, 0, 0,
                    $"تقویم ShiftDates برای ماه {persianYear}/{persianMonth:00} ناقص است " +
                    $"({monthDates.Count} از {daysInMonth} روز). ابتدا تقویم را تکمیل کنید.");
            }

            var holidayDates = shiftDates
                .Where(d => d.IsHoliday == true && d.Date.HasValue)
                .Select(d => d.Date!.Value.Date)
                .ToHashSet();

            var (nightDays, holidayWeekendNights) =
                PersianMonthNightCalendar.CountNightCapacities(monthDates, holidayDates);

            var (departmentShifts, _) = await _shiftRepository.GetByFilterAsync(
                new SimpleFilter<Shift>(s => s.DepartmentId == departmentId),
                "RequiredSpecialties");

            var (totalNightSlots, holidayWeekendNightSlots, headcountPerRegularNight, headcountPerHolidayNight) =
                DepartmentNightQuotaCapacityCalculator.Calculate(departmentShifts, monthDates, holidayDates);

            return (
                totalNightSlots,
                holidayWeekendNightSlots,
                nightDays,
                holidayWeekendNights,
                headcountPerRegularNight,
                headcountPerHolidayNight,
                null);
        }

        private static UserMonthlyNightQuotaDtoGet MapToDto(UserMonthlyNightQuota entity) => new()
        {
            Id = entity.Id ?? 0,
            UserId = entity.UserId,
            UserName = entity.User?.FullName,
            DepartmentId = entity.User?.DepartmentId,
            PersianYear = entity.PersianYear,
            PersianMonth = entity.PersianMonth,
            ExactNightShiftCount = entity.ExactNightShiftCount,
            ExactHolidayWeekendNightShiftCount = entity.ExactHolidayWeekendNightShiftCount,
            NightFallbackParticipation = entity.NightFallbackParticipation,
            HolidayWeekendNightFallbackParticipation = entity.HolidayWeekendNightFallbackParticipation
        };

        private readonly record struct NightQuotaValues(
            int? ExactNightShiftCount,
            bool? NightFallbackParticipation,
            int? ExactHolidayWeekendNightShiftCount,
            bool? HolidayWeekendNightFallbackParticipation);

        private static bool TryNormalize(UserMonthlyNightQuotaDtoAdd dto, out NightQuotaValues values, out string error) =>
            TryNormalize(
                dto.ExactNightShiftCount,
                dto.NightFallbackParticipation,
                dto.ExactHolidayWeekendNightShiftCount,
                dto.HolidayWeekendNightFallbackParticipation,
                out values,
                out error);

        private static bool TryNormalize(UserMonthlyNightQuotaItemDto item, out NightQuotaValues values, out string error) =>
            TryNormalize(
                item.ExactNightShiftCount,
                item.NightFallbackParticipation,
                item.ExactHolidayWeekendNightShiftCount,
                item.HolidayWeekendNightFallbackParticipation,
                out values,
                out error);

        private static bool TryNormalize(
            int? night,
            bool? nightFallback,
            int? holiday,
            bool? holidayFallback,
            out NightQuotaValues values,
            out string error)
        {
            error = string.Empty;

            if (night.HasValue && night.Value < 0)
            {
                values = default;
                error = "تعداد شیفت شب نمی‌تواند منفی باشد.";
                return false;
            }

            if (holiday.HasValue && holiday.Value < 0)
            {
                values = default;
                error = "تعداد شب تعطیل/آخرهفته نمی‌تواند منفی باشد.";
                return false;
            }

            if (night.HasValue && holiday.HasValue && holiday.Value > night.Value)
            {
                values = default;
                error = "تعداد شب تعطیل/آخرهفته نمی‌تواند بیشتر از تعداد کل شیفت شب باشد.";
                return false;
            }

            values = new NightQuotaValues(night, nightFallback, holiday, holidayFallback);
            return true;
        }

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


        private int? GetActorUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
