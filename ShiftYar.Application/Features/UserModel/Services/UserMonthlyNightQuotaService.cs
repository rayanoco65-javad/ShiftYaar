using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.UserModel;
using ShiftYar.Domain.Entities.UserModel;
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
        private readonly ILogger<UserMonthlyNightQuotaService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserMonthlyNightQuotaService(
            IEfRepository<UserMonthlyNightQuota> repository,
            IEfRepository<User> userRepository,
            ILogger<UserMonthlyNightQuotaService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _userRepository = userRepository;
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

            if (!TryNormalizeCounts(dto.ExactNightShiftCount, dto.ExactHolidayWeekendNightShiftCount, out var night, out var holiday, out var countError))
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail(countError);
            }

            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user == null)
            {
                return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail("کاربر یافت نشد.");
            }

            var (existing, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyNightQuota>(q =>
                    q.UserId == dto.UserId &&
                    q.PersianYear == dto.PersianYear &&
                    q.PersianMonth == dto.PersianMonth));

            var entity = existing.FirstOrDefault();
            var now = DateTime.Now;
            var actorId = GetActorUserId();

            if (entity == null)
            {
                if (!night.HasValue && !holiday.HasValue)
                {
                    return ApiResponse<UserMonthlyNightQuotaDtoGet>.Fail("حداقل یکی از مقادیر سهمیه شب باید مشخص شود.");
                }

                entity = new UserMonthlyNightQuota
                {
                    UserId = dto.UserId,
                    PersianYear = dto.PersianYear,
                    PersianMonth = dto.PersianMonth,
                    ExactNightShiftCount = night,
                    ExactHolidayWeekendNightShiftCount = holiday,
                    CreateDate = now,
                    TheUserId = actorId
                };
                await _repository.AddAsync(entity);
            }
            else
            {
                if (!night.HasValue && !holiday.HasValue)
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

                entity.ExactNightShiftCount = night;
                entity.ExactHolidayWeekendNightShiftCount = holiday;
                entity.UpdateDate = now;
                entity.TheUserId = actorId;
                _repository.Update(entity);
            }

            await _repository.SaveAsync();
            entity.User = user;
            _logger.LogInformation(
                "Upserted monthly night quota UserId={UserId} {Year}/{Month} Night={Night} Holiday={Holiday}",
                dto.UserId, dto.PersianYear, dto.PersianMonth, night, holiday);

            return ApiResponse<UserMonthlyNightQuotaDtoGet>.Success(MapToDto(entity), "سهمیه شب ماهانه ذخیره شد.");
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
            var deptUserIds = deptUsers.Where(u => u.Id.HasValue).Select(u => u.Id!.Value).ToHashSet();

            var invalidUser = dto.Items.FirstOrDefault(i => !deptUserIds.Contains(i.UserId));
            if (invalidUser != null)
            {
                return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(
                    $"کاربر {invalidUser.UserId} در دپارتمان {dto.DepartmentId} یافت نشد.");
            }

            var results = new List<UserMonthlyNightQuotaDtoGet>();
            foreach (var item in dto.Items)
            {
                var upsert = await UpsertQuotaAsync(new UserMonthlyNightQuotaDtoAdd
                {
                    UserId = item.UserId,
                    PersianYear = dto.PersianYear,
                    PersianMonth = dto.PersianMonth,
                    ExactNightShiftCount = item.ExactNightShiftCount,
                    ExactHolidayWeekendNightShiftCount = item.ExactHolidayWeekendNightShiftCount
                });

                if (!upsert.IsSuccess)
                {
                    return ApiResponse<List<UserMonthlyNightQuotaDtoGet>>.Fail(
                        $"خطا برای کاربر {item.UserId}: {upsert.Message}");
                }

                if (upsert.Data != null && (item.ExactNightShiftCount.HasValue || item.ExactHolidayWeekendNightShiftCount.HasValue))
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

            _repository.Delete(entity);
            await _repository.SaveAsync();
            return ApiResponse<string>.Success("حذف شد.", "سهمیه شب ماهانه حذف شد.");
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
            ExactHolidayWeekendNightShiftCount = entity.ExactHolidayWeekendNightShiftCount
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

        private static bool TryNormalizeCounts(
            int? night,
            int? holiday,
            out int? normalizedNight,
            out int? normalizedHoliday,
            out string error)
        {
            normalizedNight = night;
            normalizedHoliday = holiday;
            error = string.Empty;

            if (night.HasValue && night.Value < 0)
            {
                error = "تعداد شیفت شب نمی‌تواند منفی باشد.";
                return false;
            }

            if (holiday.HasValue && holiday.Value < 0)
            {
                error = "تعداد شب تعطیل/آخرهفته نمی‌تواند منفی باشد.";
                return false;
            }

            if (night.HasValue && holiday.HasValue && holiday.Value > night.Value)
            {
                error = "تعداد شب تعطیل/آخرهفته نمی‌تواند بیشتر از تعداد کل شیفت شب باشد.";
                return false;
            }

            return true;
        }

        private int? GetActorUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
