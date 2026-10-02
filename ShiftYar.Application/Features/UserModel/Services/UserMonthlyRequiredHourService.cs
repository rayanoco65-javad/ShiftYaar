using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ProductivityModel;
using ShiftYar.Application.Interfaces.UserModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ProductivityModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.UserModel.Services
{
    public class UserMonthlyRequiredHourService : IUserMonthlyRequiredHourService
    {
        private readonly IEfRepository<UserMonthlyRequiredHour> _repository;
        private readonly IEfRepository<User> _userRepository;
        private readonly IEfRepository<Department> _departmentRepository;
        private readonly IWorkingHoursCalculator _workingHoursCalculator;
        private readonly ICalendarHolidayProvider _calendarHolidayProvider;
        private readonly ILogger<UserMonthlyRequiredHourService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserMonthlyRequiredHourService(
            IEfRepository<UserMonthlyRequiredHour> repository,
            IEfRepository<User> userRepository,
            IEfRepository<Department> departmentRepository,
            IWorkingHoursCalculator workingHoursCalculator,
            ICalendarHolidayProvider calendarHolidayProvider,
            ILogger<UserMonthlyRequiredHourService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _userRepository = userRepository;
            _departmentRepository = departmentRepository;
            _workingHoursCalculator = workingHoursCalculator;
            _calendarHolidayProvider = calendarHolidayProvider;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<DepartmentMonthlyRequiredHoursPreviewDto>> GetDepartmentMonthlyRequiredHoursPreviewAsync(
            int departmentId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var monthError))
            {
                return ApiResponse<DepartmentMonthlyRequiredHoursPreviewDto>.Fail(monthError);
            }

            var department = await _departmentRepository.GetByIdAsync(departmentId);
            if (department == null)
            {
                return ApiResponse<DepartmentMonthlyRequiredHoursPreviewDto>.Fail("دپارتمان موردنظر یافت نشد.");
            }

            var activeUsersResult = await _userRepository.GetByFilterAsync(
                new UserFilter
                {
                    DepartmentId = departmentId,
                    IsActive = true,
                    PageNumber = 1,
                    PageSize = 5000
                },
                includes: new[] { "Department" });

            var activeUsers = activeUsersResult.Items
                .Where(u => u.Id.HasValue && u.IsActive == true)
                .OrderBy(u => u.FullName)
                .ToList();

            var monthWorkingDaysInfo = await _calendarHolidayProvider.GetMonthWorkingDaysInfoAsync(persianYear, persianMonth);
            var (monthStart, _, _) = PersianMonthNightCalendar.GetMonthBounds(persianYear, persianMonth);

            var (existingRecords, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyRequiredHour>(r =>
                    r.DepartmentId == departmentId &&
                    r.PersianYear == persianYear &&
                    r.PersianMonth == persianMonth),
                includes: new[] { "User", "Department" });

            var existingMap = existingRecords
                .GroupBy(r => r.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var previewItems = new List<UserMonthlyRequiredHourDtoGet>();

            foreach (var user in activeUsers)
            {
                var userId = user.Id!.Value;
                var calculation = _workingHoursCalculator.CalculateMonthlyRequiredHoursDetails(user, persianYear, persianMonth);
                var calculatedNetHours = calculation.NetMonthlyRequiredHours;
                var yearsOfService = ResolveYearsOfService(user, monthStart);

                if (existingMap.TryGetValue(userId, out var saved))
                {
                    previewItems.Add(new UserMonthlyRequiredHourDtoGet
                    {
                        Id = saved.Id,
                        UserId = userId,
                        FullName = user.FullName ?? $"کاربر {userId}",
                        PersonnelCode = user.PersonnelCode,
                        JobTitle = user.JobTitle,
                        ShiftType = user.ShiftType?.ToString(),
                        ShiftSubType = user.ShiftSubType?.ToString(),
                        DepartmentId = departmentId,
                        DepartmentName = department.Name,
                        PersianYear = persianYear,
                        PersianMonth = persianMonth,
                        CalculatedHours = calculatedNetHours,
                        ApprovedHours = saved.ApprovedHours,
                        IsSaved = true,
                        IsManuallyEdited = saved.IsManuallyEdited,
                        Notes = saved.Notes,
                        ConfirmedAt = saved.ConfirmedAt ?? saved.UpdateDate ?? saved.CreateDate,
                        ConfirmedByUserId = saved.ConfirmedByUserId,
                        YearsOfService = yearsOfService,
                        HardshipPercent = user.HardshipPercent,
                        WeeklyReductionHours = calculation.WeeklyProductivityReduction,
                        WorkingDaysCount = calculation.WorkingDaysCount
                    });
                }
                else
                {
                    previewItems.Add(new UserMonthlyRequiredHourDtoGet
                    {
                        Id = null,
                        UserId = userId,
                        FullName = user.FullName ?? $"کاربر {userId}",
                        PersonnelCode = user.PersonnelCode,
                        JobTitle = user.JobTitle,
                        ShiftType = user.ShiftType?.ToString(),
                        ShiftSubType = user.ShiftSubType?.ToString(),
                        DepartmentId = departmentId,
                        DepartmentName = department.Name,
                        PersianYear = persianYear,
                        PersianMonth = persianMonth,
                        CalculatedHours = calculatedNetHours,
                        ApprovedHours = calculatedNetHours, // مقدار پیش‌فرض برابر با ساعت محاسبه‌شده سیستم است
                        IsSaved = false,
                        IsManuallyEdited = false,
                        Notes = null,
                        ConfirmedAt = null,
                        ConfirmedByUserId = null,
                        YearsOfService = yearsOfService,
                        HardshipPercent = user.HardshipPercent,
                        WeeklyReductionHours = calculation.WeeklyProductivityReduction,
                        WorkingDaysCount = calculation.WorkingDaysCount
                    });
                }
            }

            var totalActive = activeUsers.Count;
            var totalConfirmed = previewItems.Count(p => p.IsSaved);

            var previewDto = new DepartmentMonthlyRequiredHoursPreviewDto
            {
                DepartmentId = departmentId,
                DepartmentName = department.Name,
                PersianYear = persianYear,
                PersianMonth = persianMonth,
                TotalActiveUsers = totalActive,
                TotalConfirmedUsers = totalConfirmed,
                AllUsersConfirmed = totalActive > 0 && totalConfirmed == totalActive,
                TotalDaysInMonth = monthWorkingDaysInfo.TotalDays,
                WorkingDaysCount = monthWorkingDaysInfo.WorkingDaysCount,
                Users = previewItems
            };

            return ApiResponse<DepartmentMonthlyRequiredHoursPreviewDto>.Success(previewDto);
        }

        public async Task<ApiResponse<DepartmentRequiredHoursStatusDto>> CheckDepartmentRequiredHoursStatusAsync(
            int departmentId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var monthError))
            {
                return ApiResponse<DepartmentRequiredHoursStatusDto>.Fail(monthError);
            }

            var activeUsersResult = await _userRepository.GetByFilterAsync(
                new UserFilter
                {
                    DepartmentId = departmentId,
                    IsActive = true,
                    PageNumber = 1,
                    PageSize = 5000
                });

            var activeUsers = activeUsersResult.Items
                .Where(u => u.Id.HasValue && u.IsActive == true)
                .ToList();

            var (existingRecords, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyRequiredHour>(r =>
                    r.DepartmentId == departmentId &&
                    r.PersianYear == persianYear &&
                    r.PersianMonth == persianMonth));

            var confirmedUserIds = existingRecords.Select(r => r.UserId).ToHashSet();
            var missingUsers = activeUsers
                .Where(u => u.Id.HasValue && !confirmedUserIds.Contains(u.Id.Value))
                .Select(u => u.FullName ?? $"کاربر {u.Id}")
                .ToList();

            var isComplete = activeUsers.Count > 0 && missingUsers.Count == 0;
            var message = isComplete
                ? "ساعات موظفی تمام پرسنل فعال این بخش برای ماه مورد نظر تأیید شده است."
                : $"ساعات موظفی {missingUsers.Count} نفر از پرسنل فعال برای ماه {persianYear}/{persianMonth:00} تأیید نشده است.";

            var statusDto = new DepartmentRequiredHoursStatusDto
            {
                DepartmentId = departmentId,
                PersianYear = persianYear,
                PersianMonth = persianMonth,
                TotalActiveUsers = activeUsers.Count,
                ConfirmedUsersCount = existingRecords.Count,
                IsComplete = isComplete,
                MissingUsers = missingUsers,
                Message = message
            };

            return ApiResponse<DepartmentRequiredHoursStatusDto>.Success(statusDto);
        }

        public async Task<ApiResponse<List<UserMonthlyRequiredHourDtoGet>>> BulkUpsertDepartmentRequiredHoursAsync(
            UserMonthlyRequiredHourBulkUpsertDto dto)
        {
            if (!IsValidMonth(dto.PersianYear, dto.PersianMonth, out var monthError))
            {
                return ApiResponse<List<UserMonthlyRequiredHourDtoGet>>.Fail(monthError);
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return ApiResponse<List<UserMonthlyRequiredHourDtoGet>>.Fail("لیست پرسنل جهت ذخیره نمی‌تواند خالی باشد.");
            }

            var department = await _departmentRepository.GetByIdAsync(dto.DepartmentId);
            if (department == null)
            {
                return ApiResponse<List<UserMonthlyRequiredHourDtoGet>>.Fail("دپارتمان موردنظر یافت نشد.");
            }

            var activeUsersResult = await _userRepository.GetByFilterAsync(
                new UserFilter
                {
                    DepartmentId = dto.DepartmentId,
                    IsActive = true,
                    PageNumber = 1,
                    PageSize = 5000
                });

            var activeUsersMap = activeUsersResult.Items
                .Where(u => u.Id.HasValue)
                .ToDictionary(u => u.Id!.Value, u => u);

            var (existingRecords, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyRequiredHour>(r =>
                    r.DepartmentId == dto.DepartmentId &&
                    r.PersianYear == dto.PersianYear &&
                    r.PersianMonth == dto.PersianMonth),
                includes: new[] { "User", "Department" });

            var existingMap = existingRecords
                .GroupBy(r => r.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var actorUserId = GetActorUserId();
            var now = DateTime.UtcNow;
            var savedResults = new List<UserMonthlyRequiredHourDtoGet>();
            var (monthStart, _, _) = PersianMonthNightCalendar.GetMonthBounds(dto.PersianYear, dto.PersianMonth);

            foreach (var item in dto.Items)
            {
                if (!activeUsersMap.TryGetValue(item.UserId, out var user))
                {
                    continue; // کاربر در این بخش فعال نیست یا وجود ندارد
                }

                if (item.ApprovedHours < 0 || item.ApprovedHours > 400)
                {
                    return ApiResponse<List<UserMonthlyRequiredHourDtoGet>>.Fail(
                        $"ساعت موظفی واردشده برای کاربر {user.FullName} نامعتبر است (باید بین ۰ تا ۴۰۰ باشد).");
                }

                var calculation = _workingHoursCalculator.CalculateMonthlyRequiredHoursDetails(
                    user, dto.PersianYear, dto.PersianMonth);
                var systemCalculatedHours = calculation.NetMonthlyRequiredHours;
                var isManuallyEdited = Math.Abs(item.ApprovedHours - systemCalculatedHours) > 0.01m;
                var yearsOfService = ResolveYearsOfService(user, monthStart);

                if (existingMap.TryGetValue(item.UserId, out var existing))
                {
                    existing.CalculatedHours = systemCalculatedHours;
                    existing.ApprovedHours = item.ApprovedHours;
                    existing.IsManuallyEdited = isManuallyEdited;
                    existing.Notes = item.Notes;
                    existing.ConfirmedAt = now;
                    existing.ConfirmedByUserId = actorUserId;
                    existing.TheUserId = actorUserId;
                    existing.UpdateDate = now;

                    _repository.Update(existing);

                    savedResults.Add(MapToDto(existing, user, department.Name, calculation, yearsOfService));
                }
                else
                {
                    var newEntity = new UserMonthlyRequiredHour
                    {
                        UserId = item.UserId,
                        DepartmentId = dto.DepartmentId,
                        PersianYear = dto.PersianYear,
                        PersianMonth = dto.PersianMonth,
                        CalculatedHours = systemCalculatedHours,
                        ApprovedHours = item.ApprovedHours,
                        IsManuallyEdited = isManuallyEdited,
                        Notes = item.Notes,
                        ConfirmedAt = now,
                        ConfirmedByUserId = actorUserId,
                        TheUserId = actorUserId,
                        CreateDate = now,
                        UpdateDate = now
                    };

                    await _repository.AddAsync(newEntity);

                    savedResults.Add(MapToDto(newEntity, user, department.Name, calculation, yearsOfService));
                }
            }

            await _repository.SaveAsync();

            _logger.LogInformation(
                "Successfully bulk-upserted required hours for department {DepartmentId}, {Year}/{Month:00}. Count: {Count}",
                dto.DepartmentId, dto.PersianYear, dto.PersianMonth, savedResults.Count);

            return ApiResponse<List<UserMonthlyRequiredHourDtoGet>>.Success(savedResults);
        }

        public async Task<ApiResponse<PagedResponse<UserMonthlyRequiredHourDtoGet>>> GetRequiredHoursAsync(
            UserMonthlyRequiredHourFilter filter)
        {
            var result = await _repository.GetByFilterAsync(filter, "User", "Department");
            var items = result.Items.Select(r =>
            {
                var user = r.User;
                return new UserMonthlyRequiredHourDtoGet
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    FullName = user?.FullName,
                    PersonnelCode = user?.PersonnelCode,
                    JobTitle = user?.JobTitle,
                    ShiftType = user?.ShiftType?.ToString(),
                    ShiftSubType = user?.ShiftSubType?.ToString(),
                    DepartmentId = r.DepartmentId,
                    DepartmentName = r.Department?.Name,
                    PersianYear = r.PersianYear,
                    PersianMonth = r.PersianMonth,
                    CalculatedHours = r.CalculatedHours,
                    ApprovedHours = r.ApprovedHours,
                    IsSaved = true,
                    IsManuallyEdited = r.IsManuallyEdited,
                    Notes = r.Notes,
                    ConfirmedAt = r.ConfirmedAt ?? r.UpdateDate ?? r.CreateDate,
                    ConfirmedByUserId = r.ConfirmedByUserId,
                    HardshipPercent = user?.HardshipPercent
                };
            }).ToList();

            var paged = new PagedResponse<UserMonthlyRequiredHourDtoGet>
            {
                Items = items,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)Math.Max(1, filter.PageSize))
            };

            return ApiResponse<PagedResponse<UserMonthlyRequiredHourDtoGet>>.Success(paged);
        }

        public async Task<ApiResponse<UserMonthlyRequiredHourDtoGet>> GetRequiredHourAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id, "User", "Department");
            if (entity == null)
            {
                return ApiResponse<UserMonthlyRequiredHourDtoGet>.Fail("رکورد ساعت موظفی یافت نشد.");
            }

            var user = entity.User;
            var calculation = user != null
                ? _workingHoursCalculator.CalculateMonthlyRequiredHoursDetails(user, entity.PersianYear, entity.PersianMonth)
                : null;
            var (monthStart, _, _) = PersianMonthNightCalendar.GetMonthBounds(entity.PersianYear, entity.PersianMonth);
            var yearsOfService = ResolveYearsOfService(user, monthStart);

            return ApiResponse<UserMonthlyRequiredHourDtoGet>.Success(MapToDto(entity, user, entity.Department?.Name, calculation, yearsOfService));
        }

        public async Task<ApiResponse<UserMonthlyRequiredHourDtoGet>> GetRequiredHourByUserMonthAsync(
            int userId, int persianYear, int persianMonth)
        {
            if (!IsValidMonth(persianYear, persianMonth, out var monthError))
            {
                return ApiResponse<UserMonthlyRequiredHourDtoGet>.Fail(monthError);
            }

            var (items, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyRequiredHour>(r =>
                    r.UserId == userId &&
                    r.PersianYear == persianYear &&
                    r.PersianMonth == persianMonth),
                includes: new[] { "User", "Department" });

            var entity = items.FirstOrDefault();
            if (entity == null)
            {
                return ApiResponse<UserMonthlyRequiredHourDtoGet>.Fail("ساعت موظفی این کاربر برای ماه موردنظر ثبت نشده است.");
            }

            var user = entity.User;
            var calculation = user != null
                ? _workingHoursCalculator.CalculateMonthlyRequiredHoursDetails(user, entity.PersianYear, entity.PersianMonth)
                : null;
            var (monthStart, _, _) = PersianMonthNightCalendar.GetMonthBounds(entity.PersianYear, entity.PersianMonth);
            var yearsOfService = ResolveYearsOfService(user, monthStart);

            return ApiResponse<UserMonthlyRequiredHourDtoGet>.Success(MapToDto(entity, user, entity.Department?.Name, calculation, yearsOfService));
        }

        private static UserMonthlyRequiredHourDtoGet MapToDto(
            UserMonthlyRequiredHour entity,
            User? user,
            string? departmentName,
            MonthlyCalendarWorkingHoursResultDto? calculation,
            int? yearsOfService)
        {
            return new UserMonthlyRequiredHourDtoGet
            {
                Id = entity.Id,
                UserId = entity.UserId,
                FullName = user?.FullName ?? $"کاربر {entity.UserId}",
                PersonnelCode = user?.PersonnelCode,
                JobTitle = user?.JobTitle,
                ShiftType = user?.ShiftType?.ToString(),
                ShiftSubType = user?.ShiftSubType?.ToString(),
                DepartmentId = entity.DepartmentId,
                DepartmentName = departmentName ?? entity.Department?.Name,
                PersianYear = entity.PersianYear,
                PersianMonth = entity.PersianMonth,
                CalculatedHours = entity.CalculatedHours,
                ApprovedHours = entity.ApprovedHours,
                IsSaved = true,
                IsManuallyEdited = entity.IsManuallyEdited,
                Notes = entity.Notes,
                ConfirmedAt = entity.ConfirmedAt ?? entity.UpdateDate ?? entity.CreateDate,
                ConfirmedByUserId = entity.ConfirmedByUserId,
                YearsOfService = yearsOfService,
                HardshipPercent = user?.HardshipPercent,
                WeeklyReductionHours = calculation?.WeeklyProductivityReduction,
                WorkingDaysCount = calculation?.WorkingDaysCount
            };
        }

        private static int? ResolveYearsOfService(User? user, DateTime referenceDate)
        {
            if (user?.DateOfEmployment == null) return null;
            var staff = new StaffEmploymentInfo { DateOfEmployment = user.DateOfEmployment };
            return staff.ResolveYearsOfService(referenceDate);
        }

        private static bool IsValidMonth(int year, int month, out string error)
        {
            if (year < 1300 || year > 1500)
            {
                error = "سال شمسی معتبر نیست.";
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
