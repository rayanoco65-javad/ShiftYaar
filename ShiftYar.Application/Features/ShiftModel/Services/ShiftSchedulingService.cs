using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ShiftModel;
using ShiftYar.Application.Interfaces.ProductivityModel;
using ShiftYar.Application.Interfaces.Settings;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ProductivityModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.UserModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;
using ShiftYar.Application.Features.ShiftModel.OrTools.Models;
using ShiftYar.Application.Features.ShiftModel.OrTools;
using ShiftYar.Application.Features.ShiftModel.Hybrid;
using System.Globalization;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.Features.ShiftModel.Filters;
using ShiftYar.Application.Features.ShiftModel.Jobs;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Features.UserModel.Services;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.IO;
using System.Text.Json;

namespace ShiftYar.Application.Features.ShiftModel.Services
{
    /// <summary>
    /// سرویس بهینه‌سازی شیفت‌بندی با الگوریتم Simulated Annealing
    /// </summary>
    public class ShiftSchedulingService : IShiftSchedulingService
    {
        private readonly IEfRepository<User> _userRepository;
        private readonly IEfRepository<UserMonthlyNightQuota> _monthlyNightQuotaRepository;
        private readonly IEfRepository<UserMonthlyDayShiftQuota> _monthlyDayShiftQuotaRepository;
        private readonly IEfRepository<UserMonthlyComboShiftQuota> _monthlyComboShiftQuotaRepository;
        private readonly IEfRepository<Shift> _shiftRepository;
        private readonly IEfRepository<Department> _departmentRepository;
        private readonly IEfRepository<DepartmentSchedulingSettings> _deptSettingsRepository;
        private readonly IEfRepository<Specialty> _specialtyRepository;
        private readonly IEfRepository<ShiftRequiredSpecialty> _shiftRequiredSpecialtyRepository;
        private readonly IEfRepository<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest> _shiftRequestRepository; // مخزن درخواست‌های شیفت
        private readonly IEfRepository<ShiftAssignment> _shiftAssignmentRepository;
        private readonly IEfRepository<ShiftDate> _shiftDateRepository;
        private readonly IAlgorithmSettingsService _algorithmSettingsService;
        private readonly ILogger<ShiftSchedulingService> _logger;
        private readonly IWorkingHoursCalculator _workingHoursCalculator;
        private readonly IMapper _mapper;
        private readonly ISchedulingJobStore _schedulingJobStore;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ShiftSchedulingService(
            IEfRepository<User> userRepository,
            IEfRepository<UserMonthlyNightQuota> monthlyNightQuotaRepository,
            IEfRepository<UserMonthlyDayShiftQuota> monthlyDayShiftQuotaRepository,
            IEfRepository<UserMonthlyComboShiftQuota> monthlyComboShiftQuotaRepository,
            IEfRepository<Shift> shiftRepository,
            IEfRepository<Department> departmentRepository,
            IEfRepository<DepartmentSchedulingSettings> deptSettingsRepository,
            IEfRepository<Specialty> specialtyRepository,
            IEfRepository<ShiftRequiredSpecialty> shiftRequiredSpecialtyRepository,
            IEfRepository<ShiftAssignment> shiftAssignmentRepository,
            IEfRepository<ShiftDate> shiftDateRepository,
            IEfRepository<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest> shiftRequestRepository,
            IAlgorithmSettingsService algorithmSettingsService,
            IWorkingHoursCalculator workingHoursCalculator,
            IMapper mapper,
            ISchedulingJobStore schedulingJobStore,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ShiftSchedulingService> logger)
        {
            _userRepository = userRepository;
            _monthlyNightQuotaRepository = monthlyNightQuotaRepository;
            _monthlyDayShiftQuotaRepository = monthlyDayShiftQuotaRepository;
            _monthlyComboShiftQuotaRepository = monthlyComboShiftQuotaRepository;
            _shiftRepository = shiftRepository;
            _departmentRepository = departmentRepository;
            _deptSettingsRepository = deptSettingsRepository;
            _specialtyRepository = specialtyRepository;
            _shiftRequiredSpecialtyRepository = shiftRequiredSpecialtyRepository;
            _shiftAssignmentRepository = shiftAssignmentRepository;
            _shiftDateRepository = shiftDateRepository;
            _shiftRequestRepository = shiftRequestRepository;
            _algorithmSettingsService = algorithmSettingsService;
            _workingHoursCalculator = workingHoursCalculator;
            _mapper = mapper;
            _schedulingJobStore = schedulingJobStore;
            _httpContextAccessor = httpContextAccessor;

            _logger = logger;
        }

        /// <summary>
        /// اجرای الگوریتم بهینه‌سازی شیفت‌بندی
        /// </summary>
        public async Task<ApiResponse<ShiftSchedulingResultDto>> OptimizeShiftScheduleAsync(
            ShiftSchedulingRequestDto request, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting shift scheduling optimization for department {DepartmentId} using algorithm {Algorithm}",
                    request.DepartmentId, request.Algorithm);

                // بارگذاری داده‌های مورد نیاز
                var constraints = await LoadConstraintsAsync(request);
                if (constraints == null)
                {
                    return ApiResponse<ShiftSchedulingResultDto>.Fail("Failed to load constraints");
                }

                // جلوگیری از اجرای زمان‌بندی در صورت وجود درخواست‌های در وضعیت Pending در بازه هدف
                var pendingExists = await _shiftRequestRepository.ExistsAsync(x =>
                    x.Status == Domain.Enums.ShiftRequestModel.RequestStatus.Pending &&
                    x.User != null &&
                    x.User.DepartmentId == request.DepartmentId &&
                    x.RequestDate >= DateConverter.ConvertToGregorianDate(request.StartDate) &&
                    x.RequestDate <= DateConverter.ConvertToGregorianDate(request.EndDate)
                );
                if (pendingExists)
                {
                    return ApiResponse<ShiftSchedulingResultDto>.Fail("There are pending shift requests in the selected period. Resolve them before scheduling.");
                }

                ShiftSchedulingResultDto result;

                switch (request.Algorithm)
                {
                    case SchedulingAlgorithm.SimulatedAnnealing:
                        // بارگذاری پارامترها از DB در صورت NULL بودن
                        await ApplyAlgorithmSettingsFromDbAsync(request);
                        result = await OptimizeWithSimulatedAnnealingAsync(request, constraints, cancellationToken);
                        break;
                    case SchedulingAlgorithm.OrToolsCPSat:
                        await ApplyAlgorithmSettingsFromDbAsync(request);
                        result = await OptimizeWithOrToolsAsync(request, constraints, cancellationToken);
                        break;
                    case SchedulingAlgorithm.Hybrid:
                        await ApplyAlgorithmSettingsFromDbAsync(request);
                        result = await OptimizeWithHybridAsync(request, constraints, cancellationToken);
                        break;
                    default:
                        result = await OptimizeWithSimulatedAnnealingAsync(request, constraints, cancellationToken);
                        break;
                }

                _logger.LogInformation("Shift scheduling optimization completed. Final score: {Score}, Algorithm: {Algorithm}",
                    result.FinalScore, result.AlgorithmUsed);

                return ApiResponse<ShiftSchedulingResultDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during shift scheduling optimization");
                return ApiResponse<ShiftSchedulingResultDto>.Fail($"Error: {ex.Message}");
            }
        }

        /// اجرای الگوریتم بهینه‌سازی شیفت‌بندی (نسخه داخلی با تاریخ میلادی)
        public async Task<ApiResponse<ShiftSchedulingResultDto>> OptimizeShiftScheduleInternalAsync(
            ShiftSchedulingRequestInternalDto request, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting shift scheduling optimization for department {DepartmentId} using algorithm {Algorithm}",
                    request.DepartmentId, request.Algorithm);

                // بارگذاری داده‌های مورد نیاز
                var constraints = await LoadConstraintsInternalAsync(request);
                if (constraints == null)
                {
                    return ApiResponse<ShiftSchedulingResultDto>.Fail("Failed to load constraints");
                }

                ShiftSchedulingResultDto result;

                switch (request.Algorithm)
                {
                    case SchedulingAlgorithm.SimulatedAnnealing:
                        result = await OptimizeWithSimulatedAnnealingInternalAsync(request, constraints, cancellationToken);
                        break;
                    case SchedulingAlgorithm.OrToolsCPSat:
                        result = await OptimizeWithOrToolsInternalAsync(request, constraints, cancellationToken);
                        break;
                    case SchedulingAlgorithm.Hybrid:
                        result = await OptimizeWithHybridInternalAsync(request, constraints, cancellationToken);
                        break;
                    default:
                        result = await OptimizeWithSimulatedAnnealingInternalAsync(request, constraints, cancellationToken);
                        break;
                }

                _logger.LogInformation("Shift scheduling optimization completed. Final score: {Score}, Algorithm: {Algorithm}",
                    result.FinalScore, result.AlgorithmUsed);

                return ApiResponse<ShiftSchedulingResultDto>.Success(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Shift scheduling optimization timed out or was canceled for department {DepartmentId}", request.DepartmentId);
                return ApiResponse<ShiftSchedulingResultDto>.Fail("زمان پردازش الگوریتم بهینه‌سازی فراتر از سقف مجاز رفت. لطفاً درخواست را به صورت پس‌زمینه (Background Job) ثبت فرمایید.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during shift scheduling optimization");
                return ApiResponse<ShiftSchedulingResultDto>.Fail($"Error: {ex.Message}");
            }
        }


        /// اجرای کامل فرآیند بهینه‌سازی و ذخیره (اعتبارسنجی + بهینه‌سازی + ذخیره)
        /// این متد هم توسط اکشن همزمان و هم توسط اجرای پس‌زمینه استفاده می‌شود تا منطق تکرار نشود.
        public async Task<ApiResponse<object>> OptimizeAndSaveAsync(
            ShiftSchedulingRequestDto request,
            bool isBackgroundExecution = false,
            string backgroundJobId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await ReportJobProgressAsync(backgroundJobId, "Validating constraints...");

                // تبدیل تاریخ‌های شمسی به میلادی (نرمال‌شده به ابتدای روز، Unspecified)
                var internalRequest = new ShiftSchedulingRequestInternalDto
                {
                    DepartmentId = request.DepartmentId,
                    StartDate = DateTime.SpecifyKind(DateConverter.ConvertToGregorianDate(request.StartDate).Date, DateTimeKind.Unspecified),
                    EndDate = DateTime.SpecifyKind(DateConverter.ConvertToGregorianDate(request.EndDate).Date, DateTimeKind.Unspecified),
                    Algorithm = request.Algorithm,
                    AllowExtendedSolverTime = isBackgroundExecution
                };

                var scheduleGuardError = await GetMonthlyScheduleCreationBlockerAsync(
                    request.DepartmentId,
                    internalRequest.StartDate,
                    internalRequest.EndDate);
                if (scheduleGuardError != null)
                {
                    return ApiResponse<object>.Fail(scheduleGuardError);
                }

                await AutoDeleteExistingMonthlyScheduleIfEnabledAsync(
                    request.DepartmentId,
                    internalRequest.StartDate,
                    internalRequest.EndDate);

                // اعتبارسنجی اولیه
                var validationResult = await ValidateConstraintsAsync(request, cancellationToken);
                if (!validationResult.IsSuccess || (validationResult.Data?.Count ?? 0) > 0)
                {
                    return ApiResponse<object>.Fail($"Validation failed: {string.Join(", ", validationResult.Data ?? new List<string>())}");
                }

                await ReportJobProgressAsync(backgroundJobId, "Loading department data and running optimizer...");

                // اجرای بهینه‌سازی
                var optimizationResult = await OptimizeShiftScheduleInternalAsync(internalRequest, cancellationToken);
                if (!optimizationResult.IsSuccess)
                {
                    return ApiResponse<object>.Fail(optimizationResult.Message ?? "Optimization failed");
                }

                await ReportJobProgressAsync(backgroundJobId, "Saving optimized schedule...");

                // ذخیره نتیجه — حذف قبلی باید بر اساس دپارتمان باشد، نه فقط ShiftIdهای نتیجهٔ جدید
                // (در غیر این صورت انتساب‌های قدیمی با ShiftId دپارتمان دیگر مثل ۱/۲ باقی می‌مانند و Get شیفت‌های اضافه نشان می‌دهد)
                var saveResult = await SaveOptimizedScheduleAsync(optimizationResult.Data, request.DepartmentId);
                if (!saveResult.IsSuccess)
                {
                    return ApiResponse<object>.Fail(saveResult.Message ?? "Saving optimized schedule failed");
                }

                return ApiResponse<object>.Success(new
                {
                    OptimizationResult = optimizationResult.Data,
                    SaveResult = saveResult.Data,
                    Message = "Shift schedule optimized and saved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during optimize-and-save for DepartmentId={DepartmentId}", request.DepartmentId);
                return ApiResponse<object>.Fail($"Error: {ex.Message}");
            }
        }


        /// دریافت آمارهای الگوریتم
        public async Task<ApiResponse<object>> GetAlgorithmStatisticsAsync(
            ShiftSchedulingRequestDto request, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var constraints = await LoadConstraintsAsync(request);
                if (constraints == null)
                {
                    return ApiResponse<object>.Fail("Failed to load constraints");
                }

                var saParamsFromDb = await GetAlgorithmSettingsAsync(request.DepartmentId, SchedulingAlgorithm.SimulatedAnnealing);
                var parameters = new SimulatedAnnealingParameters
                {
                    InitialTemperature = saParamsFromDb.InitialTemperature,
                    FinalTemperature = saParamsFromDb.FinalTemperature,
                    CoolingRate = saParamsFromDb.CoolingRate,
                    MaxIterations = saParamsFromDb.MaxIterations,
                    MaxIterationsWithoutImprovement = saParamsFromDb.MaxIterationsWithoutImprovement
                };

                var scheduler = new SimulatedAnnealingScheduler(constraints, parameters);
                var solution = await RunCpuBoundWithTimeoutAsync(() => scheduler.Optimize(cancellationToken), TimeSpan.FromMinutes(2), cancellationToken);
                var statistics = scheduler.GetStatistics();

                return ApiResponse<object>.Success(new
                {
                    TotalIterations = statistics.TotalIterations,
                    AcceptedMoves = statistics.AcceptedMoves,
                    RejectedMoves = statistics.RejectedMoves,
                    BestScore = statistics.BestScore,
                    ExecutionTime = statistics.ExecutionTime,
                    ScoreHistory = statistics.ScoreHistory,
                    TemperatureHistory = statistics.TemperatureHistory
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting algorithm statistics");
                return ApiResponse<object>.Fail($"Error: {ex.Message}");
            }
        }


        /// اعتبارسنجی محدودیت‌های شیفت‌بندی
        public async Task<ApiResponse<List<string>>> ValidateConstraintsAsync(
            ShiftSchedulingRequestDto request, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var validationErrors = new List<string>();

                // اعتبارسنجی تاریخ‌ها
                if (DateConverter.ConvertToGregorianDate(request.StartDate) >= DateConverter.ConvertToGregorianDate(request.EndDate))
                {
                    validationErrors.Add("Start date must be before end date");
                }

                if (DateConverter.ConvertToGregorianDate(request.EndDate) < DateTime.Today)
                {
                    validationErrors.Add("End date cannot be in the past");
                }

                // اعتبارسنجی دپارتمان
                var department = await _departmentRepository.GetByIdAsync(request.DepartmentId);
                if (department == null)
                {
                    validationErrors.Add("Department not found");
                }
                else if (department.IsActive != true)
                {
                    validationErrors.Add("Department is not active");
                }

                // اعتبارسنجی وجود کاربران فعال در دپارتمان
                var users = await _userRepository.GetByFilterAsync(
                    new UserFilter
                    {
                        DepartmentId = request.DepartmentId,
                        IsActive = true,
                        PageNumber = 1,
                        PageSize = 5000
                    },
                    includes: new[] { "Department" }
                );
                
                var activeUsers = users.Items.ToList();
                
                if (activeUsers.Count == 0)
                {
                    validationErrors.Add("No active users found in the department");
                }

                // اعتبارسنجی وجود شیفت‌های تعریف شده
                var shifts = await _shiftRepository.GetByFilterAsync(
                    new ShiftFilter
                    {
                        DepartmentId = request.DepartmentId,
                        PageNumber = 1,
                        PageSize = 500
                    },
                    includes: new[] { "Department" }
                );
                
                var departmentShifts = shifts.Items.ToList();
                
                if (departmentShifts.Count == 0)
                {
                    validationErrors.Add("No shifts defined for the department");
                }

                // اعتبارسنجی بازه زمانی (حداکثر 3 ماه)
                var startDate = DateConverter.ConvertToGregorianDate(request.StartDate);
                var endDate = DateConverter.ConvertToGregorianDate(request.EndDate);
                var daysDifference = (endDate - startDate).Days;
                
                if (daysDifference > 90) // 3 ماه
                {
                    validationErrors.Add("Scheduling period cannot exceed 3 months");
                }

                return ApiResponse<List<string>>.Success(validationErrors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during constraint validation");
                return ApiResponse<List<string>>.Fail($"Error: {ex.Message}");
            }
        }


        public async Task<ApiResponse<PagedResponse<ShiftScheduleDtoGet>>> GetFilteredShiftSchedulesAsync(ShiftScheduleFilter filter)
        {
            _logger.LogInformation("Fetching shift schedules with filter: {@Filter}", filter);

            //var result = await _shiftAssignmentRepository.GetByFilterAsync(filter, "Department", "User", "Shift");

            var result = await _shiftAssignmentRepository.GetByFilterAsync(
                filter,
                includes: new[]
                {
                    "User",
                    "User.Department",
                    "User.Specialty",
                    "Shift",
                    "ShiftDate"
                });


            var data = _mapper.Map<List<ShiftScheduleDtoGet>>(result.Items);

            var pagedResponse = new PagedResponse<ShiftScheduleDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };

            return ApiResponse<PagedResponse<ShiftScheduleDtoGet>>.Success(pagedResponse);
        }


        public async Task<ApiResponse<ShiftScheduleDtoGet>> GetByIdAsync(int id)
        {
            _logger.LogInformation("Fetching shift schedule with ID: {Id}", id);

            //var entity = await _shiftAssignmentRepository.GetByIdAsync(id, "Department", "User", "Shift");

            var entity = await _shiftAssignmentRepository.GetByIdAsync(
                id,
                includes: new[]
                {
                    "User",
                    "User.Department",
                    "User.Specialty",
                    "Shift",
                    "ShiftDate"
                });

            if (entity == null)
            {
                return ApiResponse<ShiftScheduleDtoGet>.Fail("شیفت یافت نشد.");
            }

            var dto = _mapper.Map<ShiftScheduleDtoGet>(entity);

            return ApiResponse<ShiftScheduleDtoGet>.Success(dto);
        }



        /// ذخیره نتیجه بهینه‌سازی در دیتابیس
        public async Task<ApiResponse<string>> SaveOptimizedScheduleAsync(
            ShiftSchedulingResultDto result,
            int? departmentId = null)
        {
            try
            {
                if (result.Assignments == null || result.Assignments.Count == 0)
                {
                    return ApiResponse<string>.Fail("No assignments to save");
                }

                if (result.Violations != null && result.Violations.Any(v => v.Contains("ظرفیت تکمیل نشده") || v.Contains("Under capacity")))
                {
                    var underCapacityViolations = result.Violations.Where(v => v.Contains("ظرفیت تکمیل نشده") || v.Contains("Under capacity")).ToList();
                    return ApiResponse<string>.Fail("امکان ذخیره شیفت‌بندی ناقص وجود ندارد؛ ظرفیت شیفت‌ها در برخی روزها تکمیل نشده است:\n" + string.Join("\n", underCapacityViolations));
                }

                _logger.LogInformation("Saving optimized schedule with {Count} assignments", result.Assignments.Count);

                var startDate = result.Assignments.Min(a => a.Date).Date;
                var endDate = result.Assignments.Max(a => a.Date).Date;
                var resolvedDepartmentId = departmentId ?? await ResolveDepartmentIdFromAssignmentsAsync(result);
                if (!resolvedDepartmentId.HasValue || resolvedDepartmentId.Value <= 0)
                {
                    return ApiResponse<string>.Fail(
                        "DepartmentId مشخص نیست؛ امکان پاک‌سازی انتساب‌های قبلی قبل از ذخیره وجود ندارد.");
                }

                // واکشی شناسه‌های تاریخ‌های مورد نیاز (کل بازه، بدون قطع pagination)
                var (shiftDates, shiftDatesTotal) = await _shiftDateRepository.GetByFilterAsync(
                    new Features.CalendarSeeder.Filters.ShiftDateFilter
                    {
                        PersianDateStart = ToPersianDateString(startDate),
                        PersianDateEnd = ToPersianDateString(endDate),
                        PageNumber = 1,
                        PageSize = Math.Max(5000, (endDate - startDate).Days + 10)
                    }
                );

                if (shiftDatesTotal > shiftDates.Count)
                {
                    _logger.LogWarning(
                        "SaveOptimizedSchedule: ShiftDate rows truncated ({Loaded}/{Total}) for {Start}..{End}",
                        shiftDates.Count, shiftDatesTotal, startDate, endDate);
                }

                var shiftDateMap = shiftDates
                    .Where(d => d.Date.HasValue && d.Id.HasValue)
                    .GroupBy(d => d.Date!.Value.Date)
                    .ToDictionary(g => g.Key, g => g.First().Id!.Value);

                // حذف همه انتساب‌های دپارتمان در بازه — شامل شیفت‌های اشتباه/قدیمی دپارتمان دیگر
                var (deletedCount, deletedTotal) = await DeleteDepartmentAssignmentsInRangeAsync(
                    resolvedDepartmentId.Value, startDate, endDate);
                if (deletedTotal > deletedCount)
                {
                    _logger.LogWarning(
                        "SaveOptimizedSchedule: deleted assignment list truncated ({Loaded}/{Total}) for DepartmentId={DepartmentId}",
                        deletedCount, deletedTotal, resolvedDepartmentId.Value);
                }

                // ذخیره انتساب‌های جدید
                var currentUserId = GetCurrentUserId();
                var now = DateTime.Now;
                var missingShiftDateCount = 0;

                foreach (var assignment in result.Assignments)
                {
                    var shiftAssignment = new ShiftAssignment
                    {
                        UserId = assignment.UserId,
                        ShiftId = assignment.ShiftId,
                        IsOnCall = assignment.IsOnCall,
                        Notes = "Generated by Simulated Annealing Algorithm",
                        CreateDate = now,
                        TheUserId = currentUserId
                    };

                    if (shiftDateMap.TryGetValue(assignment.Date.Date, out var sdId))
                    {
                        shiftAssignment.ShiftDateId = sdId;
                    }
                    else
                    {
                        missingShiftDateCount++;
                        _logger.LogWarning(
                            "SaveOptimizedSchedule: No ShiftDate for {Date:yyyy-MM-dd}; assignment UserId={UserId} ShiftId={ShiftId} will not appear in calendar views",
                            assignment.Date, assignment.UserId, assignment.ShiftId);
                    }

                    await _shiftAssignmentRepository.AddAsync(shiftAssignment);
                }

                await _shiftAssignmentRepository.SaveAsync();

                if (missingShiftDateCount > 0)
                {
                    return ApiResponse<string>.Fail(
                        $"Schedule partially saved: {missingShiftDateCount} assignment(s) had no matching calendar date. Seed the calendar for {ToPersianDateString(startDate)}..{ToPersianDateString(endDate)} and re-run.");
                }

                _logger.LogInformation(
                    "Successfully saved {Count} shift assignments for DepartmentId={DepartmentId} after deleting {Deleted} prior row(s)",
                    result.Assignments.Count, resolvedDepartmentId.Value, deletedCount);

                return ApiResponse<string>.Success("Schedule saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while saving optimized schedule");
                return ApiResponse<string>.Fail($"Error: {ex.Message}");
            }
        }

        private async Task<int?> ResolveDepartmentIdFromAssignmentsAsync(ShiftSchedulingResultDto result)
        {
            var shiftIds = result.Assignments.Select(a => a.ShiftId).Distinct().ToList();
            if (shiftIds.Count == 0)
            {
                return null;
            }

            var (shifts, _) = await _shiftRepository.GetByFilterAsync(
                filter: new Application.Common.Filters.SimpleFilter<Shift>(s =>
                    s.Id.HasValue && shiftIds.Contains(s.Id.Value)));

            var deptIds = shifts
                .Where(s => s.DepartmentId.HasValue && s.DepartmentId.Value > 0)
                .Select(s => s.DepartmentId!.Value)
                .Distinct()
                .ToList();
            if (deptIds.Count == 1)
            {
                return deptIds[0];
            }

            var userIds = result.Assignments.Select(a => a.UserId).Distinct().ToList();
            var (users, _) = await _userRepository.GetByFilterAsync(
                filter: new Application.Common.Filters.SimpleFilter<User>(u =>
                    u.Id.HasValue && userIds.Contains(u.Id.Value)));
            var userDeptIds = users
                .Where(u => u.DepartmentId.HasValue && u.DepartmentId.Value > 0)
                .Select(u => u.DepartmentId!.Value)
                .Distinct()
                .ToList();
            return userDeptIds.Count == 1 ? userDeptIds[0] : null;
        }

        /// <summary>
        /// حذف تمام انتساب‌های شیفت دپارتمان در یک ماه شمسی — فقط قبل از شروع آن ماه.
        /// </summary>
        public async Task<ApiResponse<object>> DeleteMonthlyScheduleAsync(DeleteMonthlyScheduleRequestDto request)
        {
            try
            {
                if (request.PersianYear < 1300 || request.PersianYear > 1500 ||
                    request.PersianMonth < 1 || request.PersianMonth > 12)
                {
                    return ApiResponse<object>.Fail("سال یا ماه شمسی نامعتبر است.");
                }

                var deptSettings = await GetDepartmentSchedulingSettingsAsync(request.DepartmentId);
                var allowCurrentMonth = deptSettings?.AllowCurrentMonthScheduling == true;

                var monthNotStartedError = GetPersianMonthNotStartedError(
                    request.PersianYear,
                    request.PersianMonth,
                    actionDescription: "حذف شیفت‌بندی",
                    allowCurrentMonthScheduling: allowCurrentMonth);
                if (monthNotStartedError != null)
                {
                    return ApiResponse<object>.Fail(monthNotStartedError);
                }

                var (monthStart, monthEnd, _) = PersianMonthNightCalendar.GetMonthBounds(
                    request.PersianYear, request.PersianMonth);

                var department = await _departmentRepository.GetByIdAsync(request.DepartmentId);
                if (department == null)
                {
                    return ApiResponse<object>.Fail("دپارتمان یافت نشد.");
                }

                var (deletedCount, totalMatched) = await DeleteDepartmentAssignmentsInRangeAsync(
                    request.DepartmentId, monthStart, monthEnd);

                if (deletedCount == 0)
                {
                    return ApiResponse<object>.Success(new
                    {
                        deletedCount = 0,
                        persianYear = request.PersianYear,
                        persianMonth = request.PersianMonth,
                        departmentId = request.DepartmentId
                    }, $"برای ماه {request.PersianYear}/{request.PersianMonth:00} شیفت‌بندی ذخیره‌شده‌ای یافت نشد.");
                }

                return ApiResponse<object>.Success(new
                {
                    deletedCount,
                    totalMatched,
                    persianYear = request.PersianYear,
                    persianMonth = request.PersianMonth,
                    departmentId = request.DepartmentId,
                    monthStart = ToPersianDateString(monthStart),
                    monthEnd = ToPersianDateString(monthEnd)
                }, $"شیفت‌بندی ماه {request.PersianYear}/{request.PersianMonth:00} با موفقیت حذف شد ({deletedCount} انتساب).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error deleting monthly schedule for DepartmentId={DepartmentId} {Year}/{Month}",
                    request.DepartmentId, request.PersianYear, request.PersianMonth);
                return ApiResponse<object>.Fail($"خطا در حذف شیفت‌بندی: {ex.Message}");
            }
        }

        /// <summary>
        /// قبل از Optimize-and-save: مگر با فلگ‌های تنظیمات دپارتمان، ماه نباید شروع شده باشد
        /// و نباید برنامه قبلی برای همان ماه وجود داشته باشد.
        /// </summary>
        public async Task<string?> GetMonthlyScheduleCreationBlockerAsync(
            int departmentId,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            var pc = new PersianCalendar();
            var startYear = pc.GetYear(rangeStart.Date);
            var startMonth = pc.GetMonth(rangeStart.Date);
            var endYear = pc.GetYear(rangeEnd.Date);
            var endMonth = pc.GetMonth(rangeEnd.Date);

            if (startYear != endYear || startMonth != endMonth)
            {
                return
                    $"بازه شیفت‌بندی باید داخل یک ماه شمسی باشد. شروع: {startYear}/{startMonth:00}، پایان: {endYear}/{endMonth:00}.";
            }

            var deptSettings = await GetDepartmentSchedulingSettingsAsync(departmentId);
            var allowCurrentMonth = deptSettings?.AllowCurrentMonthScheduling == true;
            var allowAutoReplace = deptSettings?.AllowMonthlyRescheduleWithAutoDelete == true;

            var monthNotStartedError = GetPersianMonthNotStartedError(
                startYear,
                startMonth,
                actionDescription: "شیفت‌بندی کلی",
                allowCurrentMonthScheduling: allowCurrentMonth);
            if (monthNotStartedError != null)
            {
                return monthNotStartedError;
            }

            var (monthStart, monthEnd, _) = PersianMonthNightCalendar.GetMonthBounds(startYear, startMonth);
            var hasExisting = await DepartmentHasAssignmentsInRangeAsync(departmentId, monthStart, monthEnd);
            if (hasExisting && !allowAutoReplace)
            {
                return
                    $"برای ماه شمسی {startYear}/{startMonth:00} قبلاً شیفت‌بندی ذخیره شده است. " +
                    "ابتدا با اکشن حذف شیفت‌بندی ماهانه، برنامه قبلی را حذف کنید و سپس دوباره اقدام کنید.";
            }

            return null;
        }

        /// <summary>
        /// اگر AllowMonthlyRescheduleWithAutoDelete فعال باشد، برنامه قبلی همان ماه حذف می‌شود.
        /// </summary>
        private async Task AutoDeleteExistingMonthlyScheduleIfEnabledAsync(
            int departmentId,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            var deptSettings = await GetDepartmentSchedulingSettingsAsync(departmentId);
            if (deptSettings?.AllowMonthlyRescheduleWithAutoDelete != true)
            {
                return;
            }

            var pc = new PersianCalendar();
            var year = pc.GetYear(rangeStart.Date);
            var month = pc.GetMonth(rangeStart.Date);
            var (monthStart, monthEnd, _) = PersianMonthNightCalendar.GetMonthBounds(year, month);

            var (deletedCount, _) = await DeleteDepartmentAssignmentsInRangeAsync(departmentId, monthStart, monthEnd);
            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Auto-deleted {Count} prior assignment(s) before reschedule for DepartmentId={DepartmentId} Persian {Year}/{Month}",
                    deletedCount, departmentId, year, month);
            }
        }

        private async Task<DepartmentSchedulingSettings?> GetDepartmentSchedulingSettingsAsync(int departmentId)
        {
            var result = await _deptSettingsRepository.GetByFilterAsync(
                filter: new Features.DepartmentModel.Filters.DepartmentSchedulingSettingsFilter
                {
                    DepartmentId = departmentId,
                    PageNumber = 1,
                    PageSize = 1
                },
                includes: Array.Empty<string>());

            return result.Items.FirstOrDefault();
        }

        private static string? GetPersianMonthNotStartedError(
            int persianYear,
            int persianMonth,
            string actionDescription,
            bool allowCurrentMonthScheduling = false)
        {
            var (monthStart, _, _) = PersianMonthNightCalendar.GetMonthBounds(persianYear, persianMonth);
            var today = DateTime.Today;
            if (today < monthStart.Date)
            {
                return null;
            }

            if (allowCurrentMonthScheduling && IsCurrentPersianMonth(persianYear, persianMonth))
            {
                return null;
            }

            return
                $"ماه شمسی {persianYear}/{persianMonth:00} از تاریخ {DateConverter.ConvertToPersianDate(monthStart)} آغاز شده است " +
                $"و امکان {actionDescription} برای این ماه وجود ندارد.";
        }

        private static bool IsCurrentPersianMonth(int persianYear, int persianMonth)
        {
            var pc = new PersianCalendar();
            var today = DateTime.Today;
            return pc.GetYear(today) == persianYear && pc.GetMonth(today) == persianMonth;
        }

        private async Task<bool> DepartmentHasAssignmentsInRangeAsync(
            int departmentId,
            DateTime monthStart,
            DateTime monthEnd)
        {
            var (assignments, _) = await _shiftAssignmentRepository.GetByFilterAsync(
                filter: new Application.Common.Filters.SimpleFilter<ShiftAssignment>(a =>
                    a.ShiftDateId.HasValue &&
                    a.ShiftDate != null &&
                    a.ShiftDate.Date.HasValue &&
                    a.ShiftDate.Date.Value.Date >= monthStart.Date &&
                    a.ShiftDate.Date.Value.Date <= monthEnd.Date &&
                    (
                        (a.Shift != null && a.Shift.DepartmentId == departmentId) ||
                        (a.User != null && a.User.DepartmentId == departmentId)
                    )),
                includes: new[] { "ShiftDate", "Shift", "User" });

            return assignments.Count > 0;
        }

        private async Task<(int deletedCount, int totalMatched)> DeleteDepartmentAssignmentsInRangeAsync(
            int departmentId,
            DateTime monthStart,
            DateTime monthEnd)
        {
            // هم انتساب‌های شیفت‌های همین دپارتمان، هم انتساب‌های کاربران همین دپارتمان
            // (حتی اگر اشتباهاً روی ShiftId دپارتمان دیگر ذخیره شده باشند)
            var (assignments, total) = await _shiftAssignmentRepository.GetByFilterAsync(
                filter: new Application.Common.Filters.SimpleFilter<ShiftAssignment>(a =>
                    a.ShiftDateId.HasValue &&
                    a.ShiftDate != null &&
                    a.ShiftDate.Date.HasValue &&
                    a.ShiftDate.Date.Value.Date >= monthStart.Date &&
                    a.ShiftDate.Date.Value.Date <= monthEnd.Date &&
                    (
                        (a.Shift != null && a.Shift.DepartmentId == departmentId) ||
                        (a.User != null && a.User.DepartmentId == departmentId)
                    )),
                includes: new[] { "ShiftDate", "Shift", "User" });

            if (assignments.Count == 0)
            {
                return (0, total);
            }

            foreach (var assignment in assignments)
            {
                _shiftAssignmentRepository.Delete(assignment);
            }

            await _shiftAssignmentRepository.SaveAsync();

            _logger.LogInformation(
                "Deleted {Count} assignment(s) for DepartmentId={DepartmentId} range {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}",
                assignments.Count, departmentId, monthStart.Date, monthEnd.Date);

            return (assignments.Count, total);
        }


        #region Algorithm-Specific Optimization Methods

        /// <summary>
        /// <summary>
        /// بهینه‌سازی با الگوریتم Simulated Annealing
        /// </summary>
        private async Task<ShiftSchedulingResultDto> OptimizeWithSimulatedAnnealingAsync(
            ShiftSchedulingRequestDto request,
            ShiftConstraints constraints,
            CancellationToken cancellationToken = default) // اجرای SA با پارامترهای ورودی و داده‌های DB
        {
            var saParamsFromDb = await GetAlgorithmSettingsAsync(request.DepartmentId, SchedulingAlgorithm.SimulatedAnnealing);
            var parameters = new SimulatedAnnealingParameters
            {
                InitialTemperature = saParamsFromDb.InitialTemperature,
                FinalTemperature = saParamsFromDb.FinalTemperature,
                CoolingRate = saParamsFromDb.CoolingRate,
                MaxIterations = saParamsFromDb.MaxIterations,
                MaxIterationsWithoutImprovement = saParamsFromDb.MaxIterationsWithoutImprovement
            };

            var scheduler = new SimulatedAnnealingScheduler(constraints, parameters);
            EnsureNightQuotaRequestsFeasibleOrThrow(constraints);
            EnsureConflictingApprovedRequestsOrThrow(constraints);
            var solution = await RunCpuBoundWithTimeoutAsync(
                () => scheduler.Optimize(cancellationToken),
                TimeSpan.FromMinutes(4),
                cancellationToken);
            var statistics = scheduler.GetStatistics();

            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);

            scheduler.PerformFinalManagerMixRepairSweep(solution);

            if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
            {
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
                ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
                ExactNightQuotaGuard.Enforce(solution, constraints);
            }

            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            MorningEveningBalanceGuard.Enforce(solution, constraints);
            OvertimeBalanceGuard.Enforce(solution, constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
            MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
            ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);
            ShiftEligibilityGuard.StripIneligibleAssignments(solution, constraints);
            scheduler.RefreshSolutionViolations(solution);

            EnsureApprovedRequestsOrThrow(scheduler, solution, constraints);
            EnsureExactNightQuotasOrThrow(scheduler, solution);
            EnsureExactDayShiftQuotasOrThrow(scheduler, solution);
            EnsureHardDailyRulesOrThrow(solution, constraints);
            EnsureMaxConsecutiveWorkdaysOrThrow(solution, constraints);
            ShiftManagerMixGuard.EnsureOrThrow(solution, constraints);
            EnsureSpecialtyCapacityNotExceededOrThrow(solution, constraints);
            EnsureAllShiftCoverageSatisfiedOrThrow(solution, constraints);

            var result = await ConvertSolutionToResultAsync(solution, constraints);
            result.AlgorithmUsed = SchedulingAlgorithm.SimulatedAnnealing;
            result.AlgorithmStatus = "Completed";
            result.TotalIterations = statistics.TotalIterations;
            result.ExecutionTime = statistics.ExecutionTime;

            return result;
        }

        /// <summary>
        /// بهینه‌سازی با الگوریتم OR-Tools CP-SAT
        /// </summary>
        private async Task<ShiftSchedulingResultDto> OptimizeWithOrToolsAsync(
            ShiftSchedulingRequestDto request,
            ShiftConstraints constraints,
            CancellationToken cancellationToken = default) // اجرای OR-Tools با تبدیل قیود و برگرداندن نتیجه
        {
            // تبدیل محدودیت‌ها به فرمت OR-Tools
            var ortoolsConstraints = await ConvertToOrToolsConstraintsAsync(constraints, request);

            var ortParamsFromDb = await GetOrToolsSettingsAsync(request.DepartmentId);
            var parameters = new OrToolsParameters
            {
                MaxTimeInSeconds = ortParamsFromDb.MaxTimeInSeconds,
                NumSearchWorkers = ortParamsFromDb.NumSearchWorkers,
                LogSearchProgress = ortParamsFromDb.LogSearchProgress,
                MaxSolutions = ortParamsFromDb.MaxSolutions,
                RelativeGapLimit = ortParamsFromDb.RelativeGapLimit
            };

            var scheduler = new OrToolsCPSatScheduler(ortoolsConstraints, parameters);
            EnsureNightQuotaRequestsFeasibleOrThrow(constraints);
            EnsureConflictingApprovedRequestsOrThrow(constraints);
            var solution = await RunCpuBoundWithTimeoutAsync(
                () => scheduler.Optimize(),
                TimeSpan.FromMinutes(4),
                cancellationToken);

            var saSolution = ConvertOrToolsToShiftSolution(solution);
            ApplyMandatoryConstraints(saSolution, constraints);

            var result = await ConvertSolutionToResultAsync(saSolution, constraints);
            result.AlgorithmUsed = SchedulingAlgorithm.OrToolsCPSat;
            result.AlgorithmStatus = solution.Status.ToString();
            result.ExecutionTime = solution.SolveTime;

            PopulateProductivityStatistics(result, constraints);
            return result;
        }

        /// <summary>
        /// بهینه‌سازی با الگوریتم ترکیبی
        /// </summary>
        private async Task<ShiftSchedulingResultDto> OptimizeWithHybridAsync(
            ShiftSchedulingRequestDto request,
            ShiftConstraints constraints,
            CancellationToken cancellationToken = default) // اجرای الگوریتم ترکیبی با استراتژی خواسته‌شده
        {
            // تبدیل محدودیت‌ها به فرمت OR-Tools
            var ortoolsConstraints = await ConvertToOrToolsConstraintsAsync(constraints, request);

            var saParamsFromDb = await GetAlgorithmSettingsAsync(request.DepartmentId, SchedulingAlgorithm.SimulatedAnnealing);
            var saParameters = new SimulatedAnnealingParameters
            {
                InitialTemperature = saParamsFromDb.InitialTemperature,
                FinalTemperature = saParamsFromDb.FinalTemperature,
                CoolingRate = saParamsFromDb.CoolingRate,
                MaxIterations = saParamsFromDb.MaxIterations,
                MaxIterationsWithoutImprovement = saParamsFromDb.MaxIterationsWithoutImprovement
            };

            var ortParamsFromDb = await GetOrToolsSettingsAsync(request.DepartmentId);
            var ortoolsParameters = new OrToolsParameters
            {
                MaxTimeInSeconds = ortParamsFromDb.MaxTimeInSeconds,
                NumSearchWorkers = ortParamsFromDb.NumSearchWorkers,
                LogSearchProgress = ortParamsFromDb.LogSearchProgress,
                MaxSolutions = ortParamsFromDb.MaxSolutions,
                RelativeGapLimit = ortParamsFromDb.RelativeGapLimit
            };

            var hyParamsFromDb = await GetHybridSettingsAsync(request.DepartmentId);
            var hybridParameters = new HybridParameters
            {
                Strategy = hyParamsFromDb.Strategy,
                MaxIterations = hyParamsFromDb.MaxIterations,
                ComplexityThreshold = hyParamsFromDb.ComplexityThreshold
            };

            var scheduler = new HybridScheduler(constraints, ortoolsConstraints, saParameters, ortoolsParameters, hybridParameters);
            var solution = await RunCpuBoundWithTimeoutAsync(
                () => scheduler.Optimize(),
                TimeSpan.FromMinutes(4),
                cancellationToken);
            var statistics = scheduler.GetStatistics();

            var result = await ConvertHybridSolutionToResultAsync(solution, constraints);
            result.AlgorithmUsed = SchedulingAlgorithm.Hybrid;
            result.AlgorithmStatus = "Completed";

            PopulateProductivityStatistics(result, constraints);
            return result;
        }

        #endregion

        #region Private Methods

        private int? GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            return null;
        }

        private async Task ReportJobProgressAsync(string jobId, string message)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return;
            }

            try
            {
                var job = await _schedulingJobStore.GetAsync(jobId);
                if (job == null || job.Status != SchedulingJobStatus.Running)
                {
                    return;
                }

                job.Message = message;
                await _schedulingJobStore.UpdateAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update progress for scheduling job {JobId}", jobId);
            }
        }

        private static async Task<T> RunCpuBoundWithTimeoutAsync<T>(
            Func<T> work, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default)
        {
            var workTask = Task.Run(work, cancellationToken);
            var delayTask = Task.Delay(timeout, cancellationToken);

            var completedTask = await Task.WhenAny(workTask, delayTask).ConfigureAwait(false);

            if (completedTask == delayTask)
            {
                throw new TimeoutException($"زمان بهینه‌سازی شیفت‌بندی از سقف مجاز ({timeout.TotalMinutes:0.#} دقیقه) فراتر رفت.");
            }

            return await workTask.ConfigureAwait(false);
        }

        private async Task ApplyAlgorithmSettingsFromDbAsync(ShiftSchedulingRequestDto request)
        {
            try
            {
                // پارامترهای الگوریتم از دیتابیس خوانده می‌شوند؛ نیازی به تنظیم در اینجا نیست
            }
            catch { }
        }

        private async Task<(double InitialTemperature, double FinalTemperature, double CoolingRate, int MaxIterations, int MaxIterationsWithoutImprovement)> GetAlgorithmSettingsAsync(int departmentId, SchedulingAlgorithm algo, bool forBackground = false)
        {
            try
            {
                var settingsResponse = await _algorithmSettingsService.GetSettingByDepartmentAndTypeAsync(departmentId, (int)algo);
                
                if (settingsResponse.IsSuccess && settingsResponse.Data != null)
                {
                    var settings = settingsResponse.Data;
                    var maxIterations = settings.SA_MaxIterations ?? 10000;
                    var maxWithoutImprovement = settings.SA_MaxIterationsWithoutImprovement ?? 1000;

                    if (forBackground)
                    {
                        maxIterations = Math.Min(maxIterations, MaxBackgroundSaIterations);
                        maxWithoutImprovement = Math.Min(maxWithoutImprovement, 800);
                    }

                    // CoolingRate خیلی پایین (مثل 0.95) عملاً فقط ~180 تکرار می‌دهد؛ حداقل 0.997 نگه دار
                    var coolingRate = settings.SA_CoolingRate ?? 0.997;
                    if (coolingRate < 0.99)
                    {
                        coolingRate = 0.997;
                    }

                    return (
                        settings.SA_InitialTemperature ?? 1000.0,
                        settings.SA_FinalTemperature ?? 0.1,
                        coolingRate,
                        maxIterations,
                        maxWithoutImprovement
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در دریافت تنظیمات الگوریتم از دیتابیس، از مقادیر پیش‌فرض استفاده می‌شود");
            }

            // مقادیر پیش‌فرض — CoolingRate=0.997 ≈ ۳۰۰۰ تکرار مفید تا رسیدن به دمای نهایی
            var defaultMaxIterations = forBackground ? MaxBackgroundSaIterations : 10000;
            var defaultMaxWithoutImprovement = forBackground ? 800 : 1000;
            return (1000.0, 0.1, 0.997, defaultMaxIterations, defaultMaxWithoutImprovement);
        }

        internal WorkingHoursCalculationResultDto? CalculateProductivitySnapshot(User user, UserConstraint userConstraint, ShiftConstraints constraints, DepartmentSchedulingSettings? deptSetting, double nightShiftDurationHours)
        {
            if (_workingHoursCalculator == null)
            {
                return null;
            }

            var isIncluded = user.IncludedProductivityPlan ?? (userConstraint.ShiftType == ShiftTypes.RotatingShift);

            var totalDays = Math.Max(1, (int)(constraints.EndDate.Date - constraints.StartDate.Date).TotalDays + 1);
            var fridays = 0;
            var officialHolidays = 0;
            var thursdays = 0;
            for (var d = constraints.StartDate.Date; d <= constraints.EndDate.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Friday)
                {
                    fridays++;
                }
                else if (constraints.IsHoliday(d))
                {
                    officialHolidays++;
                }
                if (d.DayOfWeek == DayOfWeek.Thursday)
                {
                    thursdays++;
                }
            }
            var workingDays = Math.Max(0, totalDays - (fridays + officialHolidays));

            var employmentDate = user.DateOfEmployment.HasValue
                ? StaffEmploymentInfo.NormalizeEmploymentDate(user.DateOfEmployment.Value)
                : (DateTime?)null;

            ShiftPatternType shiftPattern;
            if (userConstraint.ShiftType == ShiftTypes.FixedShift)
            {
                var isNight = userConstraint.AllowedShiftLabels.Contains(ShiftLabel.Night) ||
                              userConstraint.AllowedShiftPermissions.HasFlag(UserShiftPermission.Night);
                shiftPattern = isNight ? ShiftPatternType.FixedNight : ShiftPatternType.FixedDay;
            }
            else if (userConstraint.ShiftType == ShiftTypes.RotatingShift)
            {
                shiftPattern = userConstraint.ShiftSubType == ShiftSubTypes.TwoShifts
                    ? ShiftPatternType.TwoShiftRotating
                    : ShiftPatternType.ThreeShiftRotating;
            }
            else
            {
                shiftPattern = ShiftPatternType.FixedDay;
            }

            var staffInfo = new StaffEmploymentInfoDto
            {
                StaffId = user.Id ?? 0,
                StaffFullName = user.FullName,
                DateOfEmployment = employmentDate,
                ClinicalExperienceYears = userConstraint.ExperienceYears,
                IsIncludedInProductivityPlan = isIncluded,
                HardshipPercent = user.HardshipPercent ?? 0m,
                HasUncommonRotatingShifts = shiftPattern == ShiftPatternType.ThreeShiftRotating || shiftPattern == ShiftPatternType.TwoShiftRotating,
                ShiftPattern = shiftPattern
            };

            var weeks = CalculateProductivityWeeks(constraints.StartDate, constraints.EndDate);

            var request = new WorkingHoursCalculationRequestDto
            {
                Staff = staffInfo,
                TargetMonth = new DateTime(constraints.StartDate.Year, constraints.StartDate.Month, 1),
                TotalDays = totalDays,
                WorkingDays = workingDays,
                FridaysCount = fridays,
                OfficialHolidaysCount = officialHolidays,
                ThursdaysCount = thursdays,
                NumberOfWeeksInMonth = weeks,
                NightHolidayHours = 0m,
                CapBaseHoursToStandardMonth = true
            };

            try
            {
                var result = _workingHoursCalculator.CalculateMonthlyHours(request);
                _logger?.LogInformation(
                    "ProductivitySnapshot calculated for User {UserId} ({UserName}): BaseHours={BaseHours}, SeniorityRed={SeniorityRed}h, HardshipRed={HardshipRed}h, ShiftRed={ShiftRed}h, TotalWeeklyRed={WeeklyRed}h, MonthlyRed={MonthlyRed}h => FinalRequiredHours={FinalHours}",
                    user.Id,
                    user.FullName,
                    result.BaseMonthlyHours,
                    result.Breakdown?.SeniorityReductionPerWeek,
                    result.Breakdown?.HardshipReductionPerWeek,
                    result.Breakdown?.ShiftPatternReductionPerWeek,
                    result.Breakdown?.TotalWeeklyReduction,
                    result.Breakdown?.MonthlyReductionFromWeeklyAdjustments,
                    result.FinalMonthlyRequiredHours);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to calculate productivity hours for user {UserId}", user.Id);
                return null;
            }
        }

        /// <summary>
        /// تعداد هفته‌های موظفی طبق قانون بهره‌وری: هر ماه ۴ هفته (۴۴×۴=۱۷۶ ساعت پایه).
        /// بازه‌های ۲۸–۳۱ روزه همان یک ماه هستند — نباید به ۵ هفته گرد شوند.
        /// </summary>
        private static int CalculateProductivityWeeks(DateTime startDate, DateTime endDate)
        {
            const int standardWeeksPerMonth = 4;
            var totalDays = (endDate.Date - startDate.Date).TotalDays + 1;
            if (totalDays <= 0)
            {
                return 1;
            }

            if (totalDays >= 28)
            {
                return standardWeeksPerMonth;
            }

            return Math.Max(1, (int)Math.Ceiling(totalDays / 7.0));
        }

        /// <summary>
        /// بارگذاری روزهای تعطیل رسمی و جمعه‌های بازه جهت استفاده در قواعد شیفت‌بندی و محاسبه دقیق موظفی.
        /// </summary>
        private async Task LoadHolidayDatesAsync(ShiftConstraints constraints)
        {
            var scheduleStartDate = constraints.StartDate.Date;
            var scheduleEndExclusive = constraints.EndDate.Date.AddDays(1);

            try
            {
                var (rangeDates, _) = await _shiftDateRepository.GetByFilterAsync(
                    new Application.Common.Filters.SimpleFilter<ShiftDate>(d =>
                        d.Date != null &&
                        d.Date >= scheduleStartDate &&
                        d.Date < scheduleEndExclusive));

                constraints.HolidayDates = rangeDates
                    .Where(d => d.IsHoliday == true && d.Date.HasValue)
                    .Select(d => d.Date.Value.Date)
                    .ToHashSet();

                // جمعه‌ها همواره تعطیل هفتگی هستند
                for (var d = scheduleStartDate; d < scheduleEndExclusive; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Friday)
                    {
                        constraints.HolidayDates.Add(d);
                    }
                }

                // در صورتی که دیتابیس تقویم رکوردی نداشته باشد یا تعطیل رسمی غیرجمعه نداشته باشد،
                // تعطیلات رسمی را از فایل مناسبت‌های تقویم به عنوان Fallback بارگذاری می‌کنیم
                if (rangeDates.Count == 0 || !rangeDates.Any(d => d.IsHoliday == true && d.Date.HasValue && d.Date.Value.DayOfWeek != DayOfWeek.Friday))
                {
                    LoadFallbackHolidaysFromResource(constraints, scheduleStartDate, scheduleEndExclusive);
                }

                _logger.LogInformation(
                    "LoadConstraints: Loaded {HolidayCount} holiday(s) (including Fridays) in range {Start}..{End}",
                    constraints.HolidayDates.Count,
                    scheduleStartDate.ToString("yyyy-MM-dd"),
                    constraints.EndDate.ToString("yyyy-MM-dd"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LoadConstraints: failed to load holiday dates from repository; applying fallback holidays");
                for (var d = scheduleStartDate; d < scheduleEndExclusive; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Friday)
                    {
                        constraints.HolidayDates.Add(d);
                    }
                }
                LoadFallbackHolidaysFromResource(constraints, scheduleStartDate, scheduleEndExclusive);
            }
        }

        private void LoadFallbackHolidaysFromResource(ShiftConstraints constraints, DateTime startDate, DateTime endDateExclusive)
        {
            try
            {
                var persianCalendar = new PersianCalendar();
                var startYear = persianCalendar.GetYear(startDate);
                var endYear = persianCalendar.GetYear(endDateExclusive.AddDays(-1));

                for (var py = startYear; py <= endYear; py++)
                {
                    var possiblePaths = new[]
                    {
                        Path.Combine(AppContext.BaseDirectory, "Resources", "Holidays", $"holidays_{py}.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Holidays", $"holidays_{py}.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "ShiftYar.Api", "Resources", "Holidays", $"holidays_{py}.json")
                    };

                    var filePath = possiblePaths.FirstOrDefault(File.Exists);
                    if (filePath != null)
                    {
                        var json = File.ReadAllText(filePath);
                        using var doc = JsonDocument.Parse(json);
                        foreach (var element in doc.RootElement.EnumerateArray())
                        {
                            if (element.TryGetProperty("Date", out var dateProp))
                            {
                                var pDateStr = dateProp.GetString();
                                if (!string.IsNullOrWhiteSpace(pDateStr))
                                {
                                    var parts = pDateStr.Split('/');
                                    if (parts.Length == 3 &&
                                        int.TryParse(parts[0], out var y) &&
                                        int.TryParse(parts[1], out var m) &&
                                        int.TryParse(parts[2], out var d))
                                    {
                                        try
                                        {
                                            var gDate = persianCalendar.ToDateTime(y, m, d, 0, 0, 0, 0).Date;
                                            if (gDate >= startDate && gDate < endDateExclusive)
                                            {
                                                constraints.HolidayDates.Add(gDate);
                                            }
                                        }
                                        catch
                                        {
                                            // تاریخ شمسی نامعتبر احتمالی را نادیده بگیر
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LoadFallbackHolidaysFromResource encountered an issue");
            }
        }

        private static double CalculateShiftDurationHours(TimeSpan start, TimeSpan end)
        {
            var duration = (end - start).TotalHours;
            if (duration <= 0)
            {
                duration += 24;
            }

            return Math.Max(1, duration);
        }

        private static int ResolveExperienceYears(User user, DateTime referenceDate)
        {
            if (!user.DateOfEmployment.HasValue)
            {
                return 0;
            }

            var employment = StaffEmploymentInfo.NormalizeEmploymentDate(user.DateOfEmployment.Value);
            var totalMonths = (referenceDate.Year - employment.Year) * 12
                              + (referenceDate.Month - employment.Month);
            if (totalMonths < 0)
            {
                return 0;
            }

            return (int)Math.Floor(totalMonths / 12m);
        }

        private static string ToPersianDateString(DateTime date) => DateConverter.ConvertToPersianDate(date);

        /// <summary>
        /// اگر Label شیفت در دیتابیس خالی باشد، از روی ساعت شروع آن را تشخیص می‌دهد
        /// تا درخواست‌های Evening/Night به‌اشتباه روی Morning نیفتند.
        /// </summary>
        private static ShiftLabel ResolveDepartmentShiftLabel(Shift shift)
        {
            if (shift.Label.HasValue)
            {
                return shift.Label.Value;
            }

            var start = shift.StartTime ?? TimeSpan.Zero;
            if (start >= TimeSpan.FromHours(5) && start < TimeSpan.FromHours(13))
            {
                return ShiftLabel.Morning;
            }

            if (start >= TimeSpan.FromHours(13) && start < TimeSpan.FromHours(20))
            {
                return ShiftLabel.Evening;
            }

            return ShiftLabel.Night;
        }

        /// <summary>
        /// نرمال‌سازی ShiftLabel درخواست — جزئیات در <see cref="Common.Utilities.ShiftLabelResolver"/>.
        /// </summary>
        private static (ShiftLabel Label, int? ShiftId) ResolveRequestShiftMapping(
            int? rawLabelValue,
            IReadOnlyList<Shift> departmentShifts)
        {
            return Common.Utilities.ShiftLabelResolver.Resolve(
                rawLabelValue,
                departmentShifts,
                ResolveDepartmentShiftLabel);
        }

        private static bool IsValidShiftLabel(ShiftLabel label)
        {
            return Enum.IsDefined(typeof(ShiftLabel), label);
        }

        private static void EnsureLabelPermitted(UserConstraint uc, ShiftLabel label)
        {
            // درخواست تأییدشده از طریق RequiredShiftSlots برای همان تاریخ مشخص مدیریت می‌شود.
            // نباید AllowedShiftPermissions را به صورت عمومی برای تمام روزهای ماه تغییر دهیم.
        }

        private static List<string> GetApprovedRequestFailures(ShiftSchedulingResultDto result, ShiftConstraints constraints)
        {
            if (!ApprovedRequestGuard.HasAnyApprovedRequestConstraints(constraints))
            {
                return new List<string>();
            }

            // راه‌حل سبک برای اعتبارسنجی نهایی روی DTO خروجی
            var solution = new ShiftSolution();
            foreach (var a in result.Assignments)
            {
                solution.AddAssignment(a.UserId, a.ShiftId, a.Date.Date, a.ShiftLabel, a.IsOnCall);
            }

            return ApprovedRequestGuard.GetUnmetViolations(solution, constraints);
        }

        private static void EnsureApprovedRequestsOrThrow(
            SimulatedAnnealingScheduler scheduler,
            ShiftSolution solution,
            ShiftConstraints constraints)
        {
            if (!ApprovedRequestGuard.HasAnyApprovedRequestConstraints(constraints))
            {
                return;
            }

            if (!scheduler.AreApprovedRequestsSatisfied(solution, out var unmet))
            {
                throw new InvalidOperationException(
                    "درخواست‌های تأییدشده به‌طور کامل در شیفت‌بندی اعمال نشدند:\n" + string.Join("\n", unmet));
            }
        }

        private static void EnsureExactNightQuotasOrThrow(
            SimulatedAnnealingScheduler scheduler,
            ShiftSolution solution)
        {
            if (!scheduler.AreExactNightQuotasSatisfied(solution, out var unmet))
            {
                throw new InvalidOperationException(
                    "سهمیه حداقل شیفت شب برای همه کاربران اعمال نشد:\n" + string.Join("\n", unmet));
            }
        }

        private static void EnsureExactDayShiftQuotasOrThrow(
            SimulatedAnnealingScheduler scheduler,
            ShiftSolution solution)
        {
            if (!scheduler.AreExactDayShiftQuotasSatisfied(solution, out var unmet))
            {
                throw new InvalidOperationException(
                    "سهمیه حداقل شیفت صبح/عصر برای همه کاربران اعمال نشد:\n" + string.Join("\n", unmet));
            }
        }

        private static void EnsureAllShiftCoverageSatisfiedOrThrow(ShiftSolution solution, ShiftConstraints constraints)
        {
            var under = ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints);
            if (under.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "تکمیل نفرات شیفت‌ها در برخی روزها امکان‌پذیر نشد:\n" + string.Join("\n", under));
        }

        /// <summary>
        /// سقف روزانه و توالی ممنوع شب→صبح / عصر→شب نباید در خروجی نهایی باقی بمانند.
        /// مرز هر روز بر اساس تقویم هجری شمسی (۰۰:۰۰ تا ۲۳:۵۹) به صورت قید سخت (Hard Guard) اعتبارسنجی می‌شود.
        /// </summary>
        private static void EnsureHardDailyRulesOrThrow(ShiftSolution solution, ShiftConstraints constraints)
        {
            var daily = DailyDuplicateAssignmentGuard.GetViolations(solution, constraints);
            var adjacency = AdjacentShiftRestGuard.GetViolations(solution, constraints);
            var eligibility = ShiftEligibilityGuard.GetViolations(solution, constraints);

            var persianDailyViolations = new List<string>();
            if (constraints.HardRules.EnforceMaxShiftsPerDay)
            {
                var maxPerDay = Math.Max(1, constraints.GlobalConstraints.MaxShiftsPerDay);
                var groupedByPersianDate = solution.Assignments.Values
                    .GroupBy(a => new
                    {
                        a.UserId,
                        PersianDate = DateConverter.ConvertToPersianDate(a.Date)
                    })
                    .Where(g => g.Count() > maxPerDay)
                    .ToList();

                foreach (var group in groupedByPersianDate)
                {
                    var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == group.Key.UserId);
                    var userName = user?.UserName ?? $"کاربر {group.Key.UserId}";
                    var shifts = string.Join(" + ", group.Select(a => a.ShiftLabel));
                    persianDailyViolations.Add(
                        $"نقض محدودیت قطعی سقف شیفت روزانه: کاربر {group.Key.UserId} ({userName}) در تاریخ شمسی {group.Key.PersianDate} دارای {group.Count()} شیفت ({shifts}) است در حالی که حداکثر شیفت مجاز {maxPerDay} می‌باشد.");
                }
            }

            var allViolations = daily.Concat(adjacency).Concat(eligibility).Concat(persianDailyViolations).Distinct().ToList();
            if (allViolations.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "قیود سخت روزانه رعایت نشدند:\n" + string.Join("\n", allViolations));
        }

        /// <summary>
        /// سقف روزهای کاری متوالی نباید در خروجی نهایی نقض شود و در صورت نقض باید عملیات متوقف شود.
        /// </summary>
        private static void EnsureMaxConsecutiveWorkdaysOrThrow(ShiftSolution solution, ShiftConstraints constraints)
        {
            if (!constraints.HardRules.EnforceMaxConsecutiveShifts)
            {
                return;
            }

            var violations = MaxConsecutiveWorkdayRules.GetViolations(solution, constraints);
            if (violations.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "سقف روزهای کاری متوالی پرسنل رعایت نشد:\n" + string.Join("\n", violations));
        }

        private static void EnsureConflictingApprovedRequestsOrThrow(ShiftConstraints constraints)
        {
            var conflicts = ApprovedRequestGuard.GetConflictingRequiredShiftSlotViolations(constraints);
            if (conflicts.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "ترکیب درخواست‌های تأییدشده غیرممکن است:\n" + string.Join("\n", conflicts));
        }

        private static void EnsureSpecialtyCapacityNotExceededOrThrow(ShiftSolution solution, ShiftConstraints constraints)
        {
            var over = ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints);
            if (over.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "تعداد شیفت‌های اختصاص‌یافته از ظرفیت روزانه بیشتر است:\n" + string.Join("\n", over));
        }

        /// <summary>
        /// اگر درخواست‌های شب تأییدشدهٔ غیرتعطیل جا برای سهمیه تعطیل نگذارند، Optimize از ابتدا fail می‌شود.
        /// </summary>
        private static void EnsureNightQuotaRequestsFeasibleOrThrow(ShiftConstraints constraints)
        {
            var errors = new List<string>();
            foreach (var user in constraints.UserConstraints)
            {
                if (!user.ExactNightShiftCount.HasValue ||
                    !user.ExactHolidayWeekendNightShiftCount.HasValue ||
                    user.ExactHolidayWeekendNightShiftCount.Value <= 0)
                {
                    continue;
                }

                var protectedNights = user.RequiredShiftSlots
                    .Where(s => s.ShiftLabel == ShiftLabel.Night)
                    .Select(s => s.Date.Date)
                    .Distinct()
                    .ToList();

                var nonHolidayProtected = protectedNights.Count(d => !constraints.IsHolidayWeekendNight(d));
                var maxNonHoliday = Math.Max(0, user.ExactNightShiftCount.Value - user.ExactHolidayWeekendNightShiftCount.Value);
                if (nonHolidayProtected > maxNonHoliday)
                {
                    var name = string.IsNullOrWhiteSpace(user.UserName) ? $"User {user.UserId}" : user.UserName;
                    errors.Add(
                        $"{name} (UserId={user.UserId}): {nonHolidayProtected} درخواست شب غیرتعطیل تأییدشده دارد، " +
                        $"ولی با سهمیه شب {user.ExactNightShiftCount.Value} و سهمیه تعطیل {user.ExactHolidayWeekendNightShiftCount.Value} " +
                        $"حداکثر {maxNonHoliday} شب غیرتعطیل مجاز است. " +
                        "یکی از درخواست‌های شب را به تاریخ تعطیل/آخرهفته منتقل کنید یا سهمیه را اصلاح کنید.");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "ترکیب سهمیه شب و درخواست‌های تأییدشده غیرممکن است:\n" + string.Join("\n", errors));
            }
        }

        private static double CalculateDefaultNightShiftDuration(IEnumerable<ShiftRequirement> requirements)
        {
            var nightDurations = requirements
                .Where(r => r.ShiftLabel == ShiftLabel.Night && r.DurationHours > 0)
                .Select(r => r.DurationHours)
                .ToList();

            if (nightDurations.Count == 0)
            {
                return 8;
            }

            return nightDurations.Average();
        }

        private void PopulateProductivityStatistics(ShiftSchedulingResultDto result, ShiftConstraints constraints)
        {
            if (result == null || constraints == null || constraints.ShiftRequirements.Count == 0)
            {
                return;
            }

            var shiftInfoLookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
            var isInPlan = ProductivityWorkedHoursCalculator.BuildProductivityPlanLookup(constraints.UserConstraints);

            var hoursByUser = result.Assignments
                .GroupBy(a => a.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
                        g.Select(a => new SaShiftAssignment
                        {
                            UserId = a.UserId,
                            ShiftId = a.ShiftId,
                            Date = a.Date,
                            ShiftLabel = a.ShiftLabel,
                            IsOnCall = a.IsOnCall
                        }),
                        shiftInfoLookup,
                        constraints.IsHoliday,
                        isInPlan));

            result.Statistics ??= new ShiftSchedulingStatisticsDto();
            result.Statistics.WorkedHoursByUser = hoursByUser;
            result.Statistics.TotalScheduledHours = hoursByUser.Values.Sum();

            var requiredByUser = constraints.UserConstraints
                .Where(u => u.ProductivityRequiredHours.HasValue)
                .ToDictionary(u => u.UserId, u => (double)u.ProductivityRequiredHours.Value);

            result.Statistics.ProductivityRequiredHoursByUser = requiredByUser;

            var overtime = new Dictionary<int, double>();
            var shortfall = new Dictionary<int, double>();
            var compliantCount = 0;
            var fulfilledCount = 0;
            const double tolerance = 0.25;

            foreach (var user in constraints.UserConstraints.Where(u => u.ProductivityRequiredHours.HasValue))
            {
                var worked = hoursByUser.TryGetValue(user.UserId, out var value) ? value : 0;
                var required = (double)user.ProductivityRequiredHours!.Value;
                var maxAllowed = ProductivityWorkedHoursCalculator.GetMaxAllowedHours(
                    user.ProductivityRequiredHours,
                    user.OvertimeConsent,
                    user.MaxMonthlyOvertimeHours);
                var delta = worked - maxAllowed;
                if (delta > tolerance)
                {
                    overtime[user.UserId] = delta;
                }
                else
                {
                    overtime[user.UserId] = 0;
                    compliantCount++;
                }

                var gap = required - worked;
                if (gap > tolerance)
                {
                    shortfall[user.UserId] = gap;
                }
                else
                {
                    shortfall[user.UserId] = 0;
                    fulfilledCount++;
                }
            }

            result.Statistics.ProductivityOvertimeByUser = overtime;
            result.Statistics.ProductivityShortfallByUser = shortfall;
            if (requiredByUser.Count > 0)
            {
                result.Statistics.ProductivityComplianceRate = compliantCount / (double)requiredByUser.Count;
                result.Statistics.ProductivityTargetFulfillmentRate = fulfilledCount / (double)requiredByUser.Count;
            }

            if (constraints.UserConstraints.Count > 0)
            {
                result.Statistics.SoftConstraintViolationRate =
                    (double)(result.Violations?.Count ?? 0) / constraints.UserConstraints.Count;
            }
        }

        // Safe upper bounds for a solve that runs synchronously inside an HTTP request on a
        // memory-limited container (e.g. Liara). Without these caps a long solve exceeds the
        // reverse-proxy timeout and multiple CP-SAT workers exhaust RAM, both of which surface
        // to the client as a 502. These caps keep the request responsive; for longer/heavier
        // solves the scheduling should be moved to a background job instead of the request path.
        private const int MaxOrToolsSolveSeconds = 25;
        private const int MaxOrToolsSearchWorkers = 2;
        private const int MaxOrToolsBackgroundSolveSeconds = 180;
        private const int MaxBackgroundSaIterations = 4000;
        private const int MaxBackgroundHybridIterations = 2;

        private async Task<(int MaxTimeInSeconds, int NumSearchWorkers, bool LogSearchProgress, int MaxSolutions, double RelativeGapLimit)> GetOrToolsSettingsAsync(int departmentId, bool allowExtendedSolverTime = false)
        {
            int maxTimeInSeconds = 20;
            int numSearchWorkers = 1;
            bool logSearchProgress = false;
            int maxSolutions = 1;
            double relativeGapLimit = 0.01;

            try
            {
                var settingsResponse = await _algorithmSettingsService.GetSettingByDepartmentAndTypeAsync(departmentId, (int)SchedulingAlgorithm.OrToolsCPSat);

                if (settingsResponse.IsSuccess && settingsResponse.Data != null)
                {
                    var settings = settingsResponse.Data;
                    maxTimeInSeconds = settings.ORT_MaxTimeInSeconds ?? maxTimeInSeconds;
                    numSearchWorkers = settings.ORT_NumSearchWorkers ?? numSearchWorkers;
                    logSearchProgress = settings.ORT_LogSearchProgress ?? logSearchProgress;
                    maxSolutions = settings.ORT_MaxSolutions ?? maxSolutions;
                    relativeGapLimit = settings.ORT_RelativeGapLimit ?? relativeGapLimit;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در دریافت تنظیمات OR-Tools از دیتابیس، از مقادیر پیش‌فرض استفاده می‌شود");
            }

            // Clamp to keep the synchronous solve within the request/proxy budget and container memory.
            var maxAllowedSeconds = allowExtendedSolverTime ? MaxOrToolsBackgroundSolveSeconds : MaxOrToolsSolveSeconds;
            var clampedTime = Math.Clamp(maxTimeInSeconds, 1, maxAllowedSeconds);
            var clampedWorkers = Math.Clamp(numSearchWorkers, 1, MaxOrToolsSearchWorkers);
            if (clampedTime != maxTimeInSeconds || clampedWorkers != numSearchWorkers)
            {
                _logger.LogWarning(
                    "OR-Tools solver budget clamped for DepartmentId={DepartmentId}: MaxTimeInSeconds {RequestedTime}->{ClampedTime}, NumSearchWorkers {RequestedWorkers}->{ClampedWorkers} (background={IsBackground}).",
                    departmentId, maxTimeInSeconds, clampedTime, numSearchWorkers, clampedWorkers, allowExtendedSolverTime);
            }

            return (clampedTime, clampedWorkers, logSearchProgress, maxSolutions, relativeGapLimit);
        }

        private async Task<(ShiftYar.Application.Features.ShiftModel.Hybrid.HybridStrategy Strategy, int MaxIterations, double ComplexityThreshold)> GetHybridSettingsAsync(int departmentId, bool forBackground = false)
        {
            try
            {
                var settingsResponse = await _algorithmSettingsService.GetSettingByDepartmentAndTypeAsync(departmentId, (int)SchedulingAlgorithm.Hybrid);
                
                if (settingsResponse.IsSuccess && settingsResponse.Data != null)
                {
                    var settings = settingsResponse.Data;
                    var maxIterations = settings.HYB_MaxIterations ?? 5;
                    if (forBackground)
                    {
                        maxIterations = Math.Min(maxIterations, MaxBackgroundHybridIterations);
                    }

                    return (
                        settings.HYB_Strategy.HasValue ? (ShiftYar.Application.Features.ShiftModel.Hybrid.HybridStrategy)settings.HYB_Strategy.Value : ShiftYar.Application.Features.ShiftModel.Hybrid.HybridStrategy.OrToolsFirst,
                        maxIterations,
                        settings.HYB_ComplexityThreshold ?? 100.0
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در دریافت تنظیمات Hybrid از دیتابیس، از مقادیر پیش‌فرض استفاده می‌شود");
            }

            // مقادیر پیش‌فرض
            var defaultIterations = forBackground ? MaxBackgroundHybridIterations : 5;
            return (ShiftYar.Application.Features.ShiftModel.Hybrid.HybridStrategy.OrToolsFirst, defaultIterations, 100.0);
        }

        /// <summary>
        /// بارگذاری محدودیت‌ها از دیتابیس
        /// </summary>
        public async Task<ShiftConstraints> LoadConstraintsAsync(ShiftSchedulingRequestDto request) // بارگذاری قیود زمان‌بندی از DB
        {
            try
            {
                var constraints = new ShiftConstraints
                {
                    DepartmentId = request.DepartmentId,
                    StartDate = DateConverter.ConvertToGregorianDate(request.StartDate.Trim()),
                    EndDate = DateConverter.ConvertToGregorianDate(request.EndDate.Trim())
                };

                // بارگذاری روزهای تعطیل رسمی و جمعه‌های بازه قبل از پردازش کاربران و محاسبه موظفی
                await LoadHolidayDatesAsync(constraints);

                // تنظیم قوانین قطعی/اختیاری بر اساس تنظیمات دپارتمان
                var department = await _departmentRepository.GetByIdAsync(request.DepartmentId);
                if (department == null)
                {
                    _logger.LogWarning("LoadConstraints: Department not found for DepartmentId={DepartmentId}", request.DepartmentId);
                }
                if (department != null)
                {
                    // پیش‌فرض: برخی قوانین به صورت نرم در نظر گرفته می‌شوند
                    constraints.HardRules.EnforceWeeklyMaxShifts = false;
                    constraints.HardRules.EnforceNightShiftMonthlyCap = false;

                    // وزن‌دهی شب‌دوستی/شب‌گریزی دپارتمان
                    if (department.IsNightLover == true)
                    {
                        // دپارتمان شب‌دوست: سخت‌گیری کمتر روی سقف شب‌ها
                        constraints.SoftWeights.MonthlyNightCapWeight = 0.5;
                    }
                    else if (department.IsNightLover == false)
                    {
                        // دپارتمان شب‌گریز: سخت‌گیری بیشتر روی سقف شب‌ها
                        constraints.SoftWeights.MonthlyNightCapWeight = 2.0;
                    }
                }

                // بارگذاری تنظیمات دپارتمان (برای Override مقادیر عددی در سطح کاربر)
                var deptSettingEarlyResult = await _deptSettingsRepository.GetByFilterAsync(
                    filter: new Features.DepartmentModel.Filters.DepartmentSchedulingSettingsFilter
                    {
                        DepartmentId = request.DepartmentId,
                        PageNumber = 1,
                        PageSize = 1
                    },
                    includes: Array.Empty<string>()
                );
                var deptSettingEarly = deptSettingEarlyResult.Items.FirstOrDefault();
                if (deptSettingEarly == null)
                {
                    _logger.LogWarning("LoadConstraints: No DepartmentSchedulingSettings found for DepartmentId={DepartmentId}; falling back to defaults", request.DepartmentId);
                }

                // بارگذاری تنظیمات دپارتمان برای Hard Rules
                if (deptSettingEarly != null)
                {
                    // تنظیم Hard Rules بر اساس تنظیمات دپارتمان
                    constraints.HardRules.ForbidDuplicateDailyAssignments = deptSettingEarly.ForbidDuplicateDailyAssignments ?? true;
                    constraints.HardRules.EnforceMaxShiftsPerDay = deptSettingEarly.EnforceMaxShiftsPerDay ?? false;
                    constraints.HardRules.EnforceMinRestDays = deptSettingEarly.EnforceMinRestDays ?? false;
                    constraints.HardRules.EnforceMaxConsecutiveShifts = deptSettingEarly.EnforceMaxConsecutiveShifts ?? false;
                    constraints.HardRules.EnforceWeeklyMaxShifts = deptSettingEarly.EnforceWeeklyMaxShifts ?? false;
                    constraints.HardRules.EnforceNightShiftMonthlyCap = deptSettingEarly.EnforceNightShiftMonthlyCap ?? false;
                    constraints.HardRules.EnforceSpecialtyCapacity = deptSettingEarly.EnforceSpecialtyCapacity ?? true;
                    DepartmentPostNightShiftRulesApplier.Apply(constraints, deptSettingEarly);

                    // تنظیم Soft Weights
                    constraints.SoftWeights.GenderBalanceWeight = deptSettingEarly.GenderBalanceWeight ?? 1.0;
                    constraints.SoftWeights.SpecialtyPreferenceWeight = deptSettingEarly.SpecialtyPreferenceWeight ?? 1.0;
                    constraints.SoftWeights.UserUnwantedShiftWeight = deptSettingEarly.UserUnwantedShiftWeight ?? 1.0;
                    constraints.SoftWeights.UserPreferredShiftWeight = deptSettingEarly.UserPreferredShiftWeight ?? 1.0;
                    constraints.SoftWeights.WeeklyMaxWeight = deptSettingEarly.WeeklyMaxWeight ?? 1.0;
                    constraints.SoftWeights.MonthlyNightCapWeight = deptSettingEarly.MonthlyNightCapWeight ?? 1.0;
                    constraints.SoftWeights.FairShiftCountBalanceWeight = deptSettingEarly.FairShiftCountBalanceWeight ?? 2.0;
                    constraints.SoftWeights.ExtraShiftRotationWeight = deptSettingEarly.ExtraShiftRotationWeight ?? 1.0;
                    constraints.SoftWeights.ShiftLabelBalanceWeight = Math.Max(1.0, deptSettingEarly.ShiftLabelBalanceWeight ?? 1.0);
                    constraints.SoftWeights.FairWorkedHoursBalanceWeight = 8.0;
                    constraints.SoftWeights.FairNightShiftBalanceWeight = 2.5;
                    constraints.SoftWeights.MorningEveningBalanceWeight = 8.0;
                    constraints.SoftWeights.FairMorningEveningPeerWeight = 4.0;
                    constraints.SoftWeights.FairHolidayMorningEveningPeerWeight = 8.0;
                    constraints.SoftWeights.WorkdaySpreadWeight = 1.5;
                    constraints.SoftWeights.OffSpreadWeight = 1.0;
                    constraints.SoftWeights.ProductivityShortfallWeight = 8.0;
                    constraints.SoftWeights.ProductivityOvertimeWeight = 6.0;
                    constraints.EnableMorningShiftDistributionBySeniority =
                        deptSettingEarly.EnableMorningShiftDistributionBySeniority ?? false;
                    constraints.MorningShiftDistributionType = deptSettingEarly.MorningShiftDistributionType ?? 2;
                    constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight =
                        Math.Max(0.0, deptSettingEarly.MorningShiftDistributionWeight ?? 0.0);

                    constraints.EnableEveningShiftDistributionBySeniority =
                        deptSettingEarly.EnableEveningShiftDistributionBySeniority ?? false;
                    constraints.EveningShiftDistributionType = deptSettingEarly.EveningShiftDistributionType ?? 2;
                    constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight =
                        Math.Max(0.0, deptSettingEarly.EveningShiftDistributionWeight ?? 0.0);

                    constraints.EnableNightShiftDistributionBySeniority =
                        deptSettingEarly.EnableNightShiftDistributionBySeniority ?? false;
                    constraints.NightShiftDistributionType = deptSettingEarly.NightShiftDistributionType ?? 2;
                    constraints.SeniorityDistributionSlope = deptSettingEarly.SeniorityDistributionSlope ?? 1.0;
                    constraints.SoftWeights.NightShiftDistributionBySeniorityWeight =
                        Math.Max(0.0, deptSettingEarly.NightShiftDistributionWeight ?? 0.0);

                    constraints.EnableOvertimeDistributionBySeniority =
                        deptSettingEarly.EnableOvertimeDistributionBySeniority ??
                        (deptSettingEarly.OvertimePreferenceType is 0 or 1);
                    constraints.OvertimePreferenceType = deptSettingEarly.OvertimePreferenceType ?? 2;
                    constraints.SoftWeights.OvertimeDistributionWeight =
                        Math.Max(0.0, deptSettingEarly.OvertimeDistributionWeight ?? 1.0);
                    constraints.OvertimeSeniorityDistributionSlope =
                        deptSettingEarly.OvertimeSeniorityDistributionSlope ??
                        deptSettingEarly.SeniorityDistributionSlope ?? 1.0;

                    constraints.HardRules.EnforceProductivityHours = true;

                    constraints.SoftWeights.ShiftManagerRequirementWeight =
                        Math.Max(0.0, deptSettingEarly.ShiftManagerRequirementWeight ?? 0.0);

                    // سقف روزانه از تنظیمات دپارتمان (۱ یا ۲)؛ دیگر override به ۲ نمی‌شود
                    constraints.HardRules.ForbidDuplicateDailyAssignments = true;
                    constraints.HardRules.EnforceMaxShiftsPerDay = true;
                    var maxPerDay = deptSettingEarly.MaxShiftsPerDay ?? 2;
                    if (maxPerDay <= 0)
                    {
                        maxPerDay = 2;
                    }

                    constraints.GlobalConstraints.MaxShiftsPerDay = Math.Clamp(maxPerDay, 1, 2);

                    // سقف روز متوالی را سخت اجباری نکن مگر خود دپارتمان روشن کرده باشد.
                    // برنامه دستی اطفال معمولاً ۳ تا ۶ روز کار پشت‌سرهم دارد.
                }
                else
                {
                    constraints.HardRules.ForbidDuplicateDailyAssignments = true;
                    constraints.HardRules.EnforceMaxShiftsPerDay = true;
                    constraints.GlobalConstraints.MaxShiftsPerDay = 2;
                    constraints.HardRules.EnforceMaxConsecutiveShifts = false;
                }

                // بارگذاری کاربران دپارتمان
                var users = await _userRepository.GetByFilterAsync(
                    new UserFilter
                    {
                        DepartmentId = request.DepartmentId,
                        IsActive = true,
                        PageNumber = 1,
                        PageSize = 5000
                    },
                    includes: new[] { "Department", "Specialty" }
                );

                var departmentUsers = users.Items.ToList();

                _logger.LogInformation("LoadConstraints: Loaded {ActiveUserCount} active user(s) for DepartmentId={DepartmentId}", departmentUsers.Count, request.DepartmentId);
                if (departmentUsers.Count == 0)
                {
                    _logger.LogWarning("LoadConstraints: No active users found for DepartmentId={DepartmentId}", request.DepartmentId);
                }

                // سهمیه شب ماهانه بر اساس سال/ماه شمسی شروع بازه
                var persianCalendar = new PersianCalendar();
                var quotaPersianYear = persianCalendar.GetYear(constraints.StartDate);
                var quotaPersianMonth = persianCalendar.GetMonth(constraints.StartDate);
                var endPersianYear = persianCalendar.GetYear(constraints.EndDate);
                var endPersianMonth = persianCalendar.GetMonth(constraints.EndDate);
                if (endPersianYear != quotaPersianYear || endPersianMonth != quotaPersianMonth)
                {
                    _logger.LogWarning(
                        "LoadConstraints: schedule spans multiple Persian months ({StartYear}/{StartMonth} .. {EndYear}/{EndMonth}); night quotas loaded for start month only",
                        quotaPersianYear, quotaPersianMonth, endPersianYear, endPersianMonth);
                }

                var quotaUserIds = departmentUsers
                    .Where(u => u.Id.HasValue)
                    .Select(u => u.Id!.Value)
                    .ToList();

                var monthlyQuotasByUserId = new Dictionary<int, UserMonthlyNightQuota>();
                var monthlyDayShiftQuotasByUserId = new Dictionary<int, UserMonthlyDayShiftQuota>();
                var monthlyComboShiftQuotasByUserId = new Dictionary<int, UserMonthlyComboShiftQuota>();
                if (quotaUserIds.Count > 0)
                {
                    var (monthlyQuotas, _) = await _monthlyNightQuotaRepository.GetByFilterAsync(
                        new Application.Common.Filters.SimpleFilter<UserMonthlyNightQuota>(q =>
                            q.PersianYear == quotaPersianYear &&
                            q.PersianMonth == quotaPersianMonth &&
                            quotaUserIds.Contains(q.UserId)));

                    monthlyQuotasByUserId = monthlyQuotas
                        .GroupBy(q => q.UserId)
                        .ToDictionary(g => g.Key, g => g.First());

                    var (monthlyDayShiftQuotas, _) = await _monthlyDayShiftQuotaRepository.GetByFilterAsync(
                        new Application.Common.Filters.SimpleFilter<UserMonthlyDayShiftQuota>(q =>
                            q.PersianYear == quotaPersianYear &&
                            q.PersianMonth == quotaPersianMonth &&
                            quotaUserIds.Contains(q.UserId)));

                    monthlyDayShiftQuotasByUserId = monthlyDayShiftQuotas
                        .GroupBy(q => q.UserId)
                        .ToDictionary(g => g.Key, g => g.First());

                    var (monthlyComboShiftQuotas, _) = await _monthlyComboShiftQuotaRepository.GetByFilterAsync(
                        new Application.Common.Filters.SimpleFilter<UserMonthlyComboShiftQuota>(q =>
                            q.PersianYear == quotaPersianYear &&
                            q.PersianMonth == quotaPersianMonth &&
                            quotaUserIds.Contains(q.UserId)));

                    monthlyComboShiftQuotasByUserId = monthlyComboShiftQuotas
                        .GroupBy(q => q.UserId)
                        .ToDictionary(g => g.Key, g => g.First());

                    _logger.LogInformation(
                        "LoadConstraints: Loaded {NightQuotaCount} night, {DayShiftQuotaCount} day-shift, {ComboQuotaCount} combo quota(s) for Persian {Year}/{Month}",
                        monthlyQuotasByUserId.Count, monthlyDayShiftQuotasByUserId.Count, monthlyComboShiftQuotasByUserId.Count, quotaPersianYear, quotaPersianMonth);
                }

                foreach (var user in departmentUsers)
                {
                    monthlyQuotasByUserId.TryGetValue(user.Id ?? 0, out var monthQuota);
                    monthlyDayShiftQuotasByUserId.TryGetValue(user.Id ?? 0, out var dayShiftQuota);
                    monthlyComboShiftQuotasByUserId.TryGetValue(user.Id ?? 0, out var comboShiftQuota);

                    var userConstraint = new UserConstraint
                    {
                        UserId = user.Id ?? 0,
                        UserName = user.FullName ?? "",
                        Gender = user.Gender ?? UserGender.Male,
                        SpecialtyId = user.SpecialtyId ?? 0,
                        SpecialtyName = user.Specialty?.SpecialtyName ?? "",
                        CanBeShiftManager = (user.CanBeShiftManager ?? false)
                            || ShiftManagerRules.NormalizeLevel(user.ShiftManagerLevel).HasValue,
                        ShiftManagerLevel = ShiftManagerRules.NormalizeLevel(user.ShiftManagerLevel)
                            ?? ((user.CanBeShiftManager == true) ? ShiftManagerRules.Level1 : null),
                        IsActive = user.IsActive ?? true,
                        ShiftType = user.ShiftType ?? ShiftTypes.FixedShift,
                        ShiftSubType = user.ShiftSubType ?? ShiftSubTypes.FixedMorning,
                        TwoShiftRotationPattern = user.TwoShiftRotationPattern,
                        HardshipPercent = user.HardshipPercent ?? 0m,
                        OvertimeConsent = user.OvertimeConsent ?? false,
                        IsProjectPersonnel = user.IsProjectPersonnel,
                        DateOfEmployment = DateConverter.NormalizeEmploymentDate(user.DateOfEmployment),
                        ExperienceYears = ResolveExperienceYears(user, constraints.StartDate),
                        ExactNightShiftCount = monthQuota?.ExactNightShiftCount,
                        ExactHolidayWeekendNightShiftCount = monthQuota?.ExactHolidayWeekendNightShiftCount,
                        NightFallbackParticipation = monthQuota?.NightFallbackParticipation,
                        HolidayWeekendNightFallbackParticipation = monthQuota?.HolidayWeekendNightFallbackParticipation,
                        ExactMorningShiftCount = dayShiftQuota?.ExactMorningShiftCount,
                        ExactHolidayMorningShiftCount = dayShiftQuota?.ExactHolidayMorningShiftCount,
                        MorningFallbackParticipation = dayShiftQuota?.MorningFallbackParticipation,
                        MorningHolidayFallbackParticipation = dayShiftQuota?.MorningHolidayFallbackParticipation,
                        ExactEveningShiftCount = dayShiftQuota?.ExactEveningShiftCount,
                        ExactHolidayEveningShiftCount = dayShiftQuota?.ExactHolidayEveningShiftCount,
                        EveningFallbackParticipation = dayShiftQuota?.EveningFallbackParticipation,
                        EveningHolidayFallbackParticipation = dayShiftQuota?.EveningHolidayFallbackParticipation,
                        MorningEveningShiftCount = comboShiftQuota?.MorningEveningShiftCount,
                        MorningEveningFallbackParticipation = comboShiftQuota?.MorningEveningFallbackParticipation,
                        MorningEveningHolidayCount = comboShiftQuota?.MorningEveningHolidayCount,
                        MorningEveningHolidayFallback = comboShiftQuota?.MorningEveningHolidayFallback,
                        MorningNightShiftCount = comboShiftQuota?.MorningNightShiftCount,
                        MorningNightFallbackParticipation = comboShiftQuota?.MorningNightFallbackParticipation,
                        MorningNightHolidayCount = comboShiftQuota?.MorningNightHolidayCount,
                        MorningNightHolidayFallback = comboShiftQuota?.MorningNightHolidayFallback
                    };

                    if (userConstraint.MorningEveningShiftCount.HasValue &&
                        userConstraint.MorningEveningHolidayCount.HasValue &&
                        userConstraint.MorningEveningHolidayCount.Value > userConstraint.MorningEveningShiftCount.Value)
                    {
                        userConstraint.MorningEveningHolidayCount = userConstraint.MorningEveningShiftCount;
                    }

                    if (userConstraint.MorningNightShiftCount.HasValue &&
                        userConstraint.MorningNightHolidayCount.HasValue &&
                        userConstraint.MorningNightHolidayCount.Value > userConstraint.MorningNightShiftCount.Value)
                    {
                        userConstraint.MorningNightHolidayCount = userConstraint.MorningNightShiftCount;
                    }

                    if (userConstraint.ExactMorningShiftCount.HasValue &&
                        userConstraint.ExactHolidayMorningShiftCount.HasValue &&
                        userConstraint.ExactHolidayMorningShiftCount.Value > userConstraint.ExactMorningShiftCount.Value)
                    {
                        userConstraint.ExactHolidayMorningShiftCount = userConstraint.ExactMorningShiftCount;
                    }

                    if (userConstraint.ExactEveningShiftCount.HasValue &&
                        userConstraint.ExactHolidayEveningShiftCount.HasValue &&
                        userConstraint.ExactHolidayEveningShiftCount.Value > userConstraint.ExactEveningShiftCount.Value)
                    {
                        userConstraint.ExactHolidayEveningShiftCount = userConstraint.ExactEveningShiftCount;
                    }

                    if (userConstraint.ExactNightShiftCount.HasValue &&
                        userConstraint.ExactHolidayWeekendNightShiftCount.HasValue &&
                        userConstraint.ExactHolidayWeekendNightShiftCount.Value > userConstraint.ExactNightShiftCount.Value)
                    {
                        userConstraint.ExactHolidayWeekendNightShiftCount = userConstraint.ExactNightShiftCount;
                    }

                    if (userConstraint.ExactNightShiftCount.HasValue)
                    {
                        userConstraint.MaxNightShiftsPerMonth = Math.Max(
                            userConstraint.MaxNightShiftsPerMonth,
                            userConstraint.ExactNightShiftCount.Value);
                    }

                    userConstraint.AllowedShiftPermissions = ShiftEligibilityResolver
                        .ResolvePermissions(
                            user.AllowedShiftPermissions,
                            userConstraint.ShiftType,
                            userConstraint.ShiftSubType,
                            userConstraint.TwoShiftRotationPattern);
                    userConstraint.AllowedShiftLabels = ShiftEligibilityResolver
                        .GetStandaloneLabels(userConstraint.AllowedShiftPermissions)
                        .ToList();

                    userConstraint.MaxConsecutiveShifts = 2;
                    userConstraint.MinRestDaysBetweenShifts = 1; // پیش‌فرض
                    userConstraint.MaxShiftsPerWeek = 5; // پیش‌فرض
                    // با خاموش بودن شب‌متوالی: فقط شب پشت‌سرهم ممنوع است (Abs فاصله > ۱ ⇒ الگوی N / استراحت / N مجاز)
                    userConstraint.MinDaysBetweenNightShifts = 1;
                    if (constraints.HardRules.AllowNightShiftAfterNightShift)
                    {
                        // با فعال بودن شب متوالی، فاصلهٔ اجباری بین شب‌ها برداشته می‌شود
                        // (سقف طول زنجیره با MaxConsecutiveNightShifts کنترل می‌شود)
                        userConstraint.MinDaysBetweenNightShifts = 0;
                    }
                    if (!userConstraint.HasExactNightQuota)
                    {
                        userConstraint.MaxNightShiftsPerMonth = 8; // پیش‌فرض
                    }

                    // Override from department settings if enforcement is on
                    if (deptSettingEarly != null)
                    {
                        if (constraints.HardRules.EnforceMinRestDays && deptSettingEarly.MinRestDaysBetweenShifts.HasValue)
                        {
                            userConstraint.MinRestDaysBetweenShifts = Math.Max(0, deptSettingEarly.MinRestDaysBetweenShifts.Value);
                        }
                        if (deptSettingEarly.MaxConsecutiveShifts.HasValue && deptSettingEarly.MaxConsecutiveShifts.Value > 0)
                        {
                            userConstraint.MaxConsecutiveShifts = deptSettingEarly.MaxConsecutiveShifts.Value;
                        }
                        if (constraints.HardRules.EnforceWeeklyMaxShifts &&
                            deptSettingEarly.MaxShiftsPerWeek.HasValue &&
                            deptSettingEarly.MaxShiftsPerWeek.Value > 0)
                        {
                            userConstraint.MaxShiftsPerWeek = Math.Clamp(deptSettingEarly.MaxShiftsPerWeek.Value, 1, 7);
                        }
                        if (!userConstraint.HasExactNightQuota &&
                            constraints.HardRules.EnforceNightShiftMonthlyCap &&
                            deptSettingEarly.MaxNightShiftsPerMonth.HasValue)
                        {
                            userConstraint.MaxNightShiftsPerMonth = Math.Max(0, deptSettingEarly.MaxNightShiftsPerMonth.Value);
                        }
                    }

                    // بارگذاری درخواست‌های شیفت کاربر در حلقه حذف شد؛
                    // همان داده‌ها یک‌بار در انتهای متد (approvedRequests) بارگذاری می‌شود.

                    constraints.UserConstraints.Add(userConstraint);
                }

                // بارگذاری شیفت‌های دپارتمان
                var shifts = await _shiftRepository.GetByFilterAsync(
                    new ShiftFilter
                    {
                        DepartmentId = request.DepartmentId,
                        PageNumber = 1,
                        PageSize = 500
                    },
                    includes: new[] { "Department", "RequiredSpecialties", "RequiredSpecialties.Specialty" }
                );

                var departmentShifts = shifts.Items.ToList();

                _logger.LogInformation("LoadConstraints: Loaded {ShiftCount} shift(s) for DepartmentId={DepartmentId}", departmentShifts.Count, request.DepartmentId);
                if (departmentShifts.Count == 0)
                {
                    _logger.LogWarning("LoadConstraints: No shifts defined for DepartmentId={DepartmentId}", request.DepartmentId);
                }

                foreach (var shift in departmentShifts)
                {
                    var resolvedLabel = ResolveDepartmentShiftLabel(shift);
                    if (!shift.Label.HasValue)
                    {
                        _logger.LogWarning(
                            "LoadConstraints: ShiftId={ShiftId} has null Label; inferred {Label} from StartTime={StartTime}",
                            shift.Id, resolvedLabel, shift.StartTime);
                    }

                    var shiftRequirement = new ShiftRequirement
                    {
                        ShiftId = shift.Id ?? 0,
                        ShiftLabel = resolvedLabel,
                        DepartmentId = shift.DepartmentId ?? 0,
                        StartTime = shift.StartTime ?? TimeSpan.Zero,
                        EndTime = shift.EndTime ?? TimeSpan.Zero,
                        WeekdayNonProductivityHours = shift.WeekdayNonProductivityHours,
                        HolidayNonProductivityHours = shift.HolidayNonProductivityHours,
                        WeekdayProductivityPlanHours = shift.WeekdayProductivityPlanHours,
                        HolidayProductivityPlanHours = shift.HolidayProductivityPlanHours,
                        ManagerRequiredCount = Math.Max(0, shift.ManagerRequiredCount),
                        ManagerMinLevel1Count = Math.Clamp(
                            Math.Max(0, shift.ManagerMinLevel1Count),
                            0,
                            Math.Max(0, shift.ManagerRequiredCount))
                    };
                    var durationHours = CalculateShiftDurationHours(shiftRequirement.StartTime, shiftRequirement.EndTime);
                    shiftRequirement.DurationHours = durationHours;
                    shiftRequirement.DurationMinutes = (int)Math.Round(durationHours * 60);

                    // بارگذاری نیازمندی‌های تخصص
                    if (shift.RequiredSpecialties != null)
                    {
                        foreach (var reqSpecialty in shift.RequiredSpecialties)
                        {
                            var specialtyReq = new SpecialtyRequirement
                            {
                                SpecialtyId = reqSpecialty.SpecialtyId ?? 0,
                                SpecialtyName = reqSpecialty.Specialty?.SpecialtyName ?? "",
                                RequiredMaleCount = reqSpecialty.RequiredMaleCount ?? 0,
                                RequiredFemaleCount = reqSpecialty.RequiredFemaleCount ?? 0,
                                RequiredTotalCount = reqSpecialty.RequiredTottalCount ?? 0,
                                OnCallMaleCount = reqSpecialty.OnCallMaleCount ?? 0,
                                OnCallFemaleCount = reqSpecialty.OnCallFemaleCount ?? 0,
                                OnCallTotalCount = reqSpecialty.OnCallTottalCount ?? 0,
                                HolidayRequiredMaleCount = reqSpecialty.HolidayRequiredMaleCount,
                                HolidayRequiredFemaleCount = reqSpecialty.HolidayRequiredFemaleCount,
                                HolidayRequiredTotalCount = reqSpecialty.HolidayRequiredTottalCount,
                                HolidayOnCallMaleCount = reqSpecialty.HolidayOnCallMaleCount,
                                HolidayOnCallFemaleCount = reqSpecialty.HolidayOnCallFemaleCount,
                                HolidayOnCallTotalCount = reqSpecialty.HolidayOnCallTottalCount
                            };

                            shiftRequirement.SpecialtyRequirements.Add(specialtyReq);
                        }
                    }

                    constraints.ShiftRequirements.Add(shiftRequirement);
                }

                var nightShiftDuration = CalculateDefaultNightShiftDuration(constraints.ShiftRequirements);
                // User.Id is int?; the previous "u.Id != 0" guard did NOT exclude null keys
                // (a lifted nullable comparison "null != 0" is true), so ToDictionary(u => u.Id, ...)
                // threw ArgumentNullException on the first user with a null Id. That exception was
                // swallowed by the catch below and surfaced only as "Failed to load constraints".
                var userDictionary = departmentUsers
                    .Where(u => u.Id.HasValue)
                    .ToDictionary(u => u.Id.Value, u => u);

                foreach (var userConstraint in constraints.UserConstraints)
                {
                    if (userConstraint.UserId == 0 || !userConstraint.IsActive)
                    {
                        continue;
                    }

                    if (!userDictionary.TryGetValue(userConstraint.UserId, out var userEntity))
                    {
                        continue;
                    }

                    var productivitySnapshot = CalculateProductivitySnapshot(userEntity, userConstraint, constraints, deptSettingEarly, nightShiftDuration);
                    ProductivityRequiredHoursResolver.ApplyToUserConstraint(userEntity, userConstraint, productivitySnapshot);
                }

                // اعمال درخواست‌های شیفت تأییدشده (ShiftRequest) به قیود کاربر
                // بارگذاری گسترده بدون Contains(HashSet) در EF، سپس فیلتر در حافظه
                var scheduleStartDate = constraints.StartDate.Date;
                var scheduleEndExclusive = constraints.EndDate.Date.AddDays(1);
                var departmentUserIds = constraints.UserConstraints
                    .Select(u => u.UserId)
                    .Where(id => id > 0)
                    .ToHashSet();

                var (approvedInRange, approvedRequestTotal) = await _shiftRequestRepository.GetByFilterAsync(
                    filter: new Application.Common.Filters.SimpleFilter<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest>(x =>
                        x.Status == Domain.Enums.ShiftRequestModel.RequestStatus.Approved
                        && x.RequestDate != null
                        && x.RequestDate >= scheduleStartDate
                        && x.RequestDate < scheduleEndExclusive)
                );

                var approvedRequestItems = approvedInRange
                    .Where(r => r.UserId.HasValue && departmentUserIds.Contains(r.UserId.Value))
                    .ToList();

                _logger.LogInformation(
                    "LoadConstraints: Approved requests in range={InRangeTotal}, for department users={LoadedCount}, DepartmentId={DepartmentId}, {StartDate}..{EndDate}, activeUsers={UserCount}",
                    approvedRequestTotal,
                    approvedRequestItems.Count,
                    request.DepartmentId,
                    scheduleStartDate.ToString("yyyy-MM-dd"),
                    constraints.EndDate.ToString("yyyy-MM-dd"),
                    departmentUserIds.Count);

                int appliedOffFull = 0, appliedOffSlot = 0, appliedOnFull = 0, appliedOnSlot = 0, skippedIncomplete = 0;

                foreach (var req in approvedRequestItems)
                {
                    if (req.UserId == null || req.RequestDate == null)
                    {
                        skippedIncomplete++;
                        continue;
                    }

                    var uc = constraints.UserConstraints.FirstOrDefault(u => u.UserId == req.UserId);
                    if (uc == null)
                    {
                        _logger.LogWarning(
                            "LoadConstraints: Skipping approved shift request {RequestId} for UserId={UserId} (user not active or not in department)",
                            req.Id, req.UserId);
                        continue;
                    }

                    if (!req.RequestAction.HasValue || !req.RequestType.HasValue)
                    {
                        skippedIncomplete++;
                        _logger.LogWarning(
                            "LoadConstraints: Skipping approved shift request {RequestId} — RequestAction/RequestType is null",
                            req.Id);
                        continue;
                    }

                    var date = DateTime.SpecifyKind(req.RequestDate.Value.Date, DateTimeKind.Unspecified);

                    if (req.RequestAction == Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOffShift)
                    {
                        if (req.RequestType == Domain.Enums.ShiftRequestModel.RequestType.FullDay)
                        {
                            if (!uc.UnavailableDates.Any(d => d.Date == date))
                            {
                                uc.UnavailableDates.Add(date);
                                appliedOffFull++;
                                ApprovedOffNightBeforeRules.ApplyNightBeforeOffConstraint(
                                    uc, date, departmentShifts, ResolveDepartmentShiftLabel);
                                _logger.LogInformation(
                                    "LoadConstraints: OFF-FullDay UserId={UserId} Date={Date:yyyy-MM-dd} RequestId={RequestId}",
                                    uc.UserId, date, req.Id);
                            }
                        }
                        else
                        {
                            if (!req.ShiftLabel.HasValue)
                            {
                                skippedIncomplete++;
                                _logger.LogWarning(
                                    "LoadConstraints: Skipping SpecificShift OFF request {RequestId} — ShiftLabel is null",
                                    req.Id);
                                continue;
                            }

                            var (resolvedLabel, resolvedShiftId) = ResolveRequestShiftMapping(
                                (int)req.ShiftLabel.Value, departmentShifts);

                            if (!IsValidShiftLabel(resolvedLabel))
                            {
                                skippedIncomplete++;
                                _logger.LogWarning(
                                    "LoadConstraints: Skipping OFF request {RequestId} — unresolved ShiftLabel raw={Raw}",
                                    req.Id, (int)req.ShiftLabel.Value);
                                continue;
                            }

                            if ((int)req.ShiftLabel.Value != (int)resolvedLabel)
                            {
                                _logger.LogWarning(
                                    "LoadConstraints: OFF request {RequestId} ShiftLabel remapped raw={Raw} → {Label} (ShiftId={ShiftId})",
                                    req.Id, (int)req.ShiftLabel.Value, resolvedLabel, resolvedShiftId);
                            }

                            var slot = new ShiftSlotConstraint
                            {
                                Date = date,
                                ShiftLabel = resolvedLabel,
                                ShiftId = resolvedShiftId
                            };
                            if (!uc.UnavailableShiftSlots.Any(s =>
                                    s.Date.Date == slot.Date.Date &&
                                    s.ShiftLabel == slot.ShiftLabel &&
                                    s.ShiftId == slot.ShiftId))
                            {
                                uc.UnavailableShiftSlots.Add(slot);
                                appliedOffSlot++;
                                if (ApprovedOffNightBeforeRules.RequiresNightBeforeBlock(
                                        req.RequestType.Value, resolvedLabel))
                                {
                                    ApprovedOffNightBeforeRules.ApplyNightBeforeOffConstraint(
                                        uc, date, departmentShifts, ResolveDepartmentShiftLabel);
                                }

                                _logger.LogInformation(
                                    "LoadConstraints: OFF-Slot UserId={UserId} Date={Date:yyyy-MM-dd} Label={Label} ShiftId={ShiftId} RequestId={RequestId}",
                                    uc.UserId, date, slot.ShiftLabel, slot.ShiftId, req.Id);
                            }
                        }
                    }
                    else if (req.RequestAction == Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOnShift)
                    {
                        if (req.RequestType == Domain.Enums.ShiftRequestModel.RequestType.FullDay)
                        {
                            // برای پرسنل فیکس، حضور کل‌روز معادل حضور در شیفت فیکس همان کاربر است
                            if (uc.ShiftType == ShiftTypes.FixedShift)
                            {
                                var fixedLabel = uc.ShiftSubType == ShiftSubTypes.FixedEvening
                                    ? ShiftLabel.Evening
                                    : ShiftLabel.Morning;

                                var (resolvedLabel, resolvedShiftId) = ResolveRequestShiftMapping(
                                    (int)fixedLabel, departmentShifts);

                                var slot = new ShiftSlotConstraint
                                {
                                    Date = date,
                                    ShiftLabel = resolvedLabel,
                                    ShiftId = resolvedShiftId
                                };
                                if (!uc.RequiredShiftSlots.Any(s =>
                                        s.Date.Date == slot.Date.Date &&
                                        s.ShiftLabel == slot.ShiftLabel &&
                                        s.ShiftId == slot.ShiftId))
                                {
                                    uc.RequiredShiftSlots.Add(slot);
                                    uc.UnavailableShiftSlots.RemoveAll(s =>
                                        s.Date.Date == slot.Date.Date && s.ShiftLabel == slot.ShiftLabel);
                                    uc.UnavailableDates.RemoveAll(d => d.Date == slot.Date.Date);
                                    EnsureLabelPermitted(uc, slot.ShiftLabel);
                                    appliedOnSlot++;
                                    _logger.LogInformation(
                                        "LoadConstraints: Fixed user ON-FullDay mapped to {Label} UserId={UserId} Date={Date:yyyy-MM-dd} RequestId={RequestId}",
                                        slot.ShiftLabel, uc.UserId, date, req.Id);
                                }
                                continue;
                            }

                            // حضور کل‌روز در مدل کسب‌وکار برای کاربران چرخشی مجاز نیست (سقف ۱۲ ساعت / فقط صبح+عصر).
                            // داده‌های قدیمی اشتباه را نادیده می‌گیریم تا Optimize شکست نخورد.
                            skippedIncomplete++;
                            _logger.LogWarning(
                                "LoadConstraints: Skipping invalid ON-FullDay request {RequestId} UserId={UserId} Date={Date:yyyy-MM-dd} — full-day presence is not allowed for rotating staff; use SpecificShift",
                                req.Id, uc.UserId, date);
                            continue;
                        }
                        else
                        {
                            ShiftLabel resolvedLabel;
                            int? resolvedShiftId;

                            if (!req.ShiftLabel.HasValue)
                            {
                                if (uc.ShiftType == ShiftTypes.FixedShift)
                                {
                                    var fixedLabel = uc.ShiftSubType == ShiftSubTypes.FixedEvening
                                        ? ShiftLabel.Evening
                                        : ShiftLabel.Morning;
                                    var resolved = ResolveRequestShiftMapping(
                                        (int)fixedLabel, departmentShifts);
                                    resolvedLabel = resolved.Label;
                                    resolvedShiftId = resolved.ShiftId;
                                }
                                else
                                {
                                    skippedIncomplete++;
                                    _logger.LogWarning(
                                        "LoadConstraints: Skipping SpecificShift ON request {RequestId} — ShiftLabel is null",
                                        req.Id);
                                    continue;
                                }
                            }
                            else
                            {
                                var resolved = ResolveRequestShiftMapping(
                                    (int)req.ShiftLabel.Value, departmentShifts);
                                resolvedLabel = resolved.Label;
                                resolvedShiftId = resolved.ShiftId;

                                if (!IsValidShiftLabel(resolvedLabel))
                                {
                                    skippedIncomplete++;
                                    _logger.LogWarning(
                                        "LoadConstraints: Skipping ON request {RequestId} — unresolved ShiftLabel raw={Raw}",
                                        req.Id, (int)req.ShiftLabel.Value);
                                    continue;
                                }

                                if ((int)req.ShiftLabel.Value != (int)resolvedLabel)
                                {
                                    _logger.LogWarning(
                                        "LoadConstraints: ON request {RequestId} ShiftLabel remapped raw={Raw} → {Label} (ShiftId={ShiftId})",
                                        req.Id, (int)req.ShiftLabel.Value, resolvedLabel, resolvedShiftId);
                                }
                            }

                            var slot = new ShiftSlotConstraint
                            {
                                Date = date,
                                ShiftLabel = resolvedLabel,
                                ShiftId = resolvedShiftId
                            };
                            if (!uc.RequiredShiftSlots.Any(s =>
                                    s.Date.Date == slot.Date.Date &&
                                    s.ShiftLabel == slot.ShiftLabel &&
                                    s.ShiftId == slot.ShiftId))
                            {
                                uc.RequiredShiftSlots.Add(slot);
                                // ON صریح بر OFF مشتق همان روز/شیفت اولویت دارد
                                uc.UnavailableShiftSlots.RemoveAll(s =>
                                    s.Date.Date == slot.Date.Date && s.ShiftLabel == slot.ShiftLabel);
                                // و همچنین بر عدم‌حضور کل‌روز همان روز اولویت قطعی دارد
                                uc.UnavailableDates.RemoveAll(d => d.Date == slot.Date.Date);

                                // اولویت درخواست تأییدشده بر مجوزهای شیفت کاربر
                                EnsureLabelPermitted(uc, slot.ShiftLabel);

                                appliedOnSlot++;
                                _logger.LogInformation(
                                    "LoadConstraints: ON-Slot UserId={UserId} Date={Date:yyyy-MM-dd} Label={Label} ShiftId={ShiftId} RequestId={RequestId}",
                                    uc.UserId, date, slot.ShiftLabel, slot.ShiftId, req.Id);
                            }
                        }
                    }
                }

                _logger.LogInformation(
                    "LoadConstraints: Applied approved requests — OffFull={OffFull}, OffSlot={OffSlot}, OnFull={OnFull}, OnSlot={OnSlot}, SkippedIncomplete={Skipped}",
                    appliedOffFull, appliedOffSlot, appliedOnFull, appliedOnSlot, skippedIncomplete);

                // هشدار mismatch سال شمسی: درخواست‌های تأییدشده در سال دیگر برای همین ماه
                try
                {
                    var pc = new PersianCalendar();
                    var scheduleMonths = new HashSet<(int Year, int Month)>();
                    for (var d = scheduleStartDate; d < scheduleEndExclusive; d = d.AddDays(1))
                    {
                        scheduleMonths.Add((pc.GetYear(d), pc.GetMonth(d)));
                    }

                    var (nearbyApproved, _) = await _shiftRequestRepository.GetByFilterAsync(
                        filter: new Application.Common.Filters.SimpleFilter<ShiftYar.Domain.Entities.ShiftRequestModel.ShiftRequest>(x =>
                            x.Status == Domain.Enums.ShiftRequestModel.RequestStatus.Approved
                            && x.UserId != null
                            && departmentUserIds.Contains(x.UserId.Value)
                            && x.RequestDate != null
                            && (x.RequestDate < scheduleStartDate || x.RequestDate >= scheduleEndExclusive))
                    );

                    var outsideSameMonth = nearbyApproved
                        .Where(r => r.RequestDate.HasValue &&
                                    scheduleMonths.Any(m =>
                                        pc.GetMonth(r.RequestDate.Value) == m.Month &&
                                        pc.GetYear(r.RequestDate.Value) != m.Year))
                        .ToList();

                    if (outsideSameMonth.Count > 0)
                    {
                        _logger.LogError(
                            "LoadConstraints: {Count} approved request(s) fall in the SAME Persian month but DIFFERENT year than the schedule range ({Start}..{End}). Example RequestId={ExampleId} Date={ExampleDate:yyyy-MM-dd}. These are ignored. Re-create/approve requests for the scheduled Persian year.",
                            outsideSameMonth.Count,
                            scheduleStartDate.ToString("yyyy-MM-dd"),
                            constraints.EndDate.ToString("yyyy-MM-dd"),
                            outsideSameMonth[0].Id,
                            outsideSameMonth[0].RequestDate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LoadConstraints: failed while checking adjacent-year approved requests");
                }

                if (approvedRequestItems.Count > 0 &&
                    appliedOffFull + appliedOffSlot + appliedOnFull + appliedOnSlot == 0 &&
                    skippedIncomplete == approvedRequestItems.Count)
                {
                    _logger.LogError(
                        "LoadConstraints: {Count} approved request(s) found but NONE could be mapped (all incomplete).",
                        approvedRequestItems.Count);
                }

                // روزهای تعطیل بازه (تعطیلات رسمی و جمعه‌ها) قبلاً در ابتدای LoadConstraintsAsync بارگذاری شده‌اند.

                // پرسنل فیکس (صبح/عصر) باید همهٔ روزهای غیرتعطیل شیفت باشند
                // و در روزهای تعطیل هم نباید شیفت بگیرند (تعطیل = عدم‌حضور سخت)
                var appliedFixedSlots = 0;
                var appliedFixedHolidayOffs = 0;
                foreach (var uc in constraints.UserConstraints.Where(u =>
                             u.IsActive && u.ShiftType == ShiftTypes.FixedShift))
                {
                    var fixedLabel = uc.ShiftSubType == ShiftSubTypes.FixedEvening
                        ? ShiftLabel.Evening
                        : ShiftLabel.Morning;

                    for (var date = scheduleStartDate; date < scheduleEndExclusive; date = date.AddDays(1))
                    {
                        if (constraints.HolidayDates.Contains(date.Date))
                        {
                            // درخواست تأییدشدهٔ حضور در همان روز بر تعطیلی مقدم است
                            var hasApprovedPresence =
                                uc.RequiredShiftSlots.Any(s => s.Date.Date == date.Date) ||
                                uc.RequiredPresenceDates.Any(d => d.Date == date.Date);

                            if (hasApprovedPresence)
                            {
                                // اولویت قطعی حضور: پاک کردن هرگونه عدم‌حضور برای این روز تعطیل
                                uc.UnavailableDates.RemoveAll(d => d.Date == date.Date);

                                // اگر فقط RequiredPresenceDates بوده و اسلات مشخصی ثبت نشده، اسلات فیکس اضافه شود
                                if (!uc.RequiredShiftSlots.Any(s => s.Date.Date == date.Date))
                                {
                                    uc.RequiredShiftSlots.Add(new ShiftSlotConstraint
                                    {
                                        Date = date,
                                        ShiftLabel = fixedLabel
                                    });
                                    EnsureLabelPermitted(uc, fixedLabel);
                                    appliedFixedSlots++;
                                }
                            }
                            else if (!uc.UnavailableDates.Any(d => d.Date == date.Date))
                            {
                                uc.UnavailableDates.Add(date);
                                appliedFixedHolidayOffs++;
                            }

                            continue;
                        }

                        // مرخصی تأییدشده (کل‌روز یا همان شیفت) بر حضور فیکس مقدم است
                        if (uc.UnavailableDates.Any(d => d.Date == date.Date) ||
                            uc.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == fixedLabel))
                        {
                            continue;
                        }

                        if (uc.RequiredShiftSlots.Any(s => s.Date.Date == date.Date))
                        {
                            continue;
                        }

                        uc.RequiredShiftSlots.Add(new ShiftSlotConstraint
                        {
                            Date = date,
                            ShiftLabel = fixedLabel
                        });
                        appliedFixedSlots++;
                    }
                }

                if (appliedFixedSlots > 0 || appliedFixedHolidayOffs > 0)
                {
                    _logger.LogInformation(
                        "LoadConstraints: Fixed-shift staff — {SlotCount} mandatory daily slot(s), {OffCount} holiday OFF day(s)",
                        appliedFixedSlots, appliedFixedHolidayOffs);
                }

                // بارگذاری سابقه اخیر برای عدالت
                try
                {
                    int lookbackMonths = Math.Max(1, constraints.SoftWeights.FairnessLookbackMonths);
                    var lookbackStart = new DateTime(constraints.StartDate.Year, constraints.StartDate.Month, 1).AddMonths(-lookbackMonths);

                    var (lookbackDates, _) = await _shiftDateRepository.GetByFilterAsync(
                        new Features.CalendarSeeder.Filters.ShiftDateFilter
                        {
                            PersianDateStart = new System.Globalization.PersianCalendar().GetYear(lookbackStart).ToString("0000") + "/" +
                                               new System.Globalization.PersianCalendar().GetMonth(lookbackStart).ToString("00") + "/" +
                                               new System.Globalization.PersianCalendar().GetDayOfMonth(lookbackStart).ToString("00"),
                            PersianDateEnd = new System.Globalization.PersianCalendar().GetYear(constraints.StartDate.AddDays(-1)).ToString("0000") + "/" +
                                             new System.Globalization.PersianCalendar().GetMonth(constraints.StartDate.AddDays(-1)).ToString("00") + "/" +
                                             new System.Globalization.PersianCalendar().GetDayOfMonth(constraints.StartDate.AddDays(-1)).ToString("00"),
                            PageSize = 2000
                        }
                    );
                    var lookbackDateSet = new HashSet<DateTime>(lookbackDates.Where(d => d.Date.HasValue).Select(d => d.Date.Value.Date));

                    var (prevAssignments, _) = await _shiftAssignmentRepository.GetByFilterAsync(
                        new Application.Common.Filters.SimpleFilter<ShiftAssignment>(a =>
                            a.UserId.HasValue &&
                            departmentUserIds.Contains(a.UserId.Value) &&
                            a.ShiftDate != null &&
                            a.ShiftDate.Date.HasValue &&
                            lookbackDateSet.Contains(a.ShiftDate.Date.Value.Date)),
                        "ShiftDate"
                    );

                    var prevByUser = prevAssignments
                        .Where(a => a.UserId.HasValue && a.ShiftDate != null && a.ShiftDate.Date.HasValue && lookbackDateSet.Contains(a.ShiftDate.Date.Value.Date))
                        .GroupBy(a => a.UserId!.Value)
                        .ToDictionary(g => g.Key, g => g.Count());

                    foreach (var uc in constraints.UserConstraints)
                    {
                        uc.RecentTotalShifts = prevByUser.TryGetValue(uc.UserId, out var cnt) ? cnt : 0;
                        uc.RecentLabelCounts[ShiftLabel.Morning] = 0;
                        uc.RecentLabelCounts[ShiftLabel.Evening] = 0;
                        uc.RecentLabelCounts[ShiftLabel.Night] = 0;
                    }
                }
                catch { }

                // بارگذاری تنظیمات زمان‌بندی از جدول مخصوص
                var settings = await _deptSettingsRepository.GetByFilterAsync(
                    filter: new Features.DepartmentModel.Filters.DepartmentSchedulingSettingsFilter
                    {
                        DepartmentId = request.DepartmentId,
                        PageNumber = 1,
                        PageSize = 1
                    },
                    includes: Array.Empty<string>()
                );
                var deptSetting = settings.Items.FirstOrDefault();
                if (deptSetting != null)
                {
                    // Map hard rules
                    if (deptSetting.ForbidDuplicateDailyAssignments.HasValue) constraints.HardRules.ForbidDuplicateDailyAssignments = deptSetting.ForbidDuplicateDailyAssignments.Value;
                    if (deptSetting.EnforceMaxShiftsPerDay.HasValue) constraints.HardRules.EnforceMaxShiftsPerDay = deptSetting.EnforceMaxShiftsPerDay.Value;
                    if (deptSetting.EnforceMinRestDays.HasValue) constraints.HardRules.EnforceMinRestDays = deptSetting.EnforceMinRestDays.Value;
                    if (deptSetting.EnforceMaxConsecutiveShifts.HasValue) constraints.HardRules.EnforceMaxConsecutiveShifts = deptSetting.EnforceMaxConsecutiveShifts.Value;
                    if (deptSetting.EnforceWeeklyMaxShifts.HasValue) constraints.HardRules.EnforceWeeklyMaxShifts = deptSetting.EnforceWeeklyMaxShifts.Value;
                    if (deptSetting.EnforceNightShiftMonthlyCap.HasValue) constraints.HardRules.EnforceNightShiftMonthlyCap = deptSetting.EnforceNightShiftMonthlyCap.Value;
                    if (deptSetting.EnforceSpecialtyCapacity.HasValue) constraints.HardRules.EnforceSpecialtyCapacity = deptSetting.EnforceSpecialtyCapacity.Value;
                    DepartmentPostNightShiftRulesApplier.Apply(constraints, deptSetting);

                    // سقف روزانه از تنظیمات دپارتمان (۱ یا ۲)
                    constraints.HardRules.ForbidDuplicateDailyAssignments = true;
                    constraints.HardRules.EnforceMaxShiftsPerDay = true;
                    var maxPerDay = deptSetting.MaxShiftsPerDay ?? constraints.GlobalConstraints.MaxShiftsPerDay;
                    if (maxPerDay <= 0)
                    {
                        maxPerDay = 2;
                    }

                    constraints.GlobalConstraints.MaxShiftsPerDay = Math.Clamp(maxPerDay, 1, 2);
                    // EnforceMaxConsecutiveShifts همان مقدار تنظیمات دپارتمان می‌ماند (سخت اجباری نشود).

                    if (deptSetting.MaxConsecutiveNightShifts.HasValue)
                    {
                        constraints.GlobalConstraints.MaxConsecutiveNightShifts = Math.Max(1, deptSetting.MaxConsecutiveNightShifts.Value);
                    }

                    // Map soft weights
                    if (deptSetting.GenderBalanceWeight.HasValue) constraints.SoftWeights.GenderBalanceWeight = deptSetting.GenderBalanceWeight.Value;
                    if (deptSetting.SpecialtyPreferenceWeight.HasValue) constraints.SoftWeights.SpecialtyPreferenceWeight = deptSetting.SpecialtyPreferenceWeight.Value;
                    if (deptSetting.UserUnwantedShiftWeight.HasValue) constraints.SoftWeights.UserUnwantedShiftWeight = deptSetting.UserUnwantedShiftWeight.Value;
                    if (deptSetting.UserPreferredShiftWeight.HasValue) constraints.SoftWeights.UserPreferredShiftWeight = deptSetting.UserPreferredShiftWeight.Value;
                    if (deptSetting.WeeklyMaxWeight.HasValue) constraints.SoftWeights.WeeklyMaxWeight = deptSetting.WeeklyMaxWeight.Value;
                    if (deptSetting.MonthlyNightCapWeight.HasValue) constraints.SoftWeights.MonthlyNightCapWeight = deptSetting.MonthlyNightCapWeight.Value;

                    // Fairness weights
                    if (deptSetting.FairShiftCountBalanceWeight.HasValue) constraints.SoftWeights.FairShiftCountBalanceWeight = deptSetting.FairShiftCountBalanceWeight.Value;
                    if (deptSetting.ExtraShiftRotationWeight.HasValue) constraints.SoftWeights.ExtraShiftRotationWeight = deptSetting.ExtraShiftRotationWeight.Value;
                    if (deptSetting.ShiftLabelBalanceWeight.HasValue)
                        constraints.SoftWeights.ShiftLabelBalanceWeight = Math.Max(1.0, deptSetting.ShiftLabelBalanceWeight.Value);
                    if (deptSetting.FairnessLookbackMonths.HasValue) constraints.SoftWeights.FairnessLookbackMonths = deptSetting.FairnessLookbackMonths.Value;

                    // تعادل ساعت مؤثر و شیفت شب (پیش‌فرض قوی؛ قابل‌جایگزینی با وزن شب از تنظیمات)
                    constraints.SoftWeights.FairWorkedHoursBalanceWeight = Math.Max(8.0, constraints.SoftWeights.FairWorkedHoursBalanceWeight);
                    constraints.SoftWeights.FairMorningEveningPeerWeight = Math.Max(4.0, constraints.SoftWeights.FairMorningEveningPeerWeight);
                    constraints.SoftWeights.FairHolidayMorningEveningPeerWeight = Math.Max(8.0, constraints.SoftWeights.FairHolidayMorningEveningPeerWeight);
                    constraints.SoftWeights.WorkdaySpreadWeight = Math.Max(1.5, constraints.SoftWeights.WorkdaySpreadWeight);
                    constraints.SoftWeights.OffSpreadWeight = Math.Max(1.0, constraints.SoftWeights.OffSpreadWeight);
                    constraints.SoftWeights.FairNightShiftBalanceWeight = Math.Max(2.0, constraints.SoftWeights.FairNightShiftBalanceWeight);
                    constraints.SoftWeights.ProductivityShortfallWeight = Math.Max(8.0, constraints.SoftWeights.ProductivityShortfallWeight);
                    constraints.SoftWeights.ProductivityOvertimeWeight = Math.Max(6.0, constraints.SoftWeights.ProductivityOvertimeWeight);

                    constraints.EnableMorningShiftDistributionBySeniority =
                        deptSetting.EnableMorningShiftDistributionBySeniority ?? constraints.EnableMorningShiftDistributionBySeniority;
                    if (deptSetting.MorningShiftDistributionType.HasValue)
                    {
                        constraints.MorningShiftDistributionType = deptSetting.MorningShiftDistributionType.Value;
                    }

                    if (deptSetting.MorningShiftDistributionWeight.HasValue)
                    {
                        constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight =
                            Math.Max(0.0, deptSetting.MorningShiftDistributionWeight.Value);
                    }

                    constraints.EnableEveningShiftDistributionBySeniority =
                        deptSetting.EnableEveningShiftDistributionBySeniority ?? constraints.EnableEveningShiftDistributionBySeniority;
                    if (deptSetting.EveningShiftDistributionType.HasValue)
                    {
                        constraints.EveningShiftDistributionType = deptSetting.EveningShiftDistributionType.Value;
                    }

                    if (deptSetting.EveningShiftDistributionWeight.HasValue)
                    {
                        constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight =
                            Math.Max(0.0, deptSetting.EveningShiftDistributionWeight.Value);
                    }

                    constraints.EnableNightShiftDistributionBySeniority =
                        deptSetting.EnableNightShiftDistributionBySeniority ?? constraints.EnableNightShiftDistributionBySeniority;
                    if (deptSetting.NightShiftDistributionType.HasValue)
                    {
                        constraints.NightShiftDistributionType = deptSetting.NightShiftDistributionType.Value;
                    }

                    if (deptSetting.SeniorityDistributionSlope.HasValue)
                    {
                        constraints.SeniorityDistributionSlope = deptSetting.SeniorityDistributionSlope.Value;
                    }

                    if (deptSetting.NightShiftDistributionWeight.HasValue)
                    {
                        constraints.SoftWeights.NightShiftDistributionBySeniorityWeight =
                            Math.Max(0.0, deptSetting.NightShiftDistributionWeight.Value);
                    }

                    constraints.EnableOvertimeDistributionBySeniority =
                        deptSetting.EnableOvertimeDistributionBySeniority ??
                        (deptSetting.OvertimePreferenceType is 0 or 1 ? true : constraints.EnableOvertimeDistributionBySeniority);
                    if (deptSetting.OvertimePreferenceType.HasValue)
                    {
                        constraints.OvertimePreferenceType = deptSetting.OvertimePreferenceType.Value;
                    }
                    if (deptSetting.OvertimeDistributionWeight.HasValue)
                    {
                        constraints.SoftWeights.OvertimeDistributionWeight =
                            Math.Max(0.0, deptSetting.OvertimeDistributionWeight.Value);
                    }
                    if (deptSetting.OvertimeSeniorityDistributionSlope.HasValue)
                    {
                        constraints.OvertimeSeniorityDistributionSlope =
                            deptSetting.OvertimeSeniorityDistributionSlope.Value;
                    }

                    constraints.HardRules.EnforceProductivityHours = true;
                }

                _logger.LogInformation(
                    "LoadConstraints: Successfully built constraints for DepartmentId={DepartmentId} with {UserCount} user constraint(s) and {ShiftCount} shift requirement(s)",
                    request.DepartmentId, constraints.UserConstraints.Count, constraints.ShiftRequirements.Count);

                return constraints;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error loading constraints for DepartmentId={DepartmentId}, StartDate={StartDate}, EndDate={EndDate}.",
                    request.DepartmentId, request.StartDate, request.EndDate);
                // Propagate the real cause instead of collapsing it into a generic
                // "Failed to load constraints". The calling optimize methods already wrap
                // this in a try/catch and surface "Error: {message}" to the API response.
                throw new InvalidOperationException(
                    $"Failed to load constraints for DepartmentId={request.DepartmentId} (StartDate={request.StartDate}, EndDate={request.EndDate}): {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// تبدیل راه‌حل به DTO نتیجه
        /// </summary>
        private async Task<ShiftSchedulingResultDto> ConvertSolutionToResultAsync(ShiftSolution solution, ShiftConstraints constraints) // نگاشت راه‌حل SA به DTO خروجی
        {
            var freshViolations = new List<string>();
            freshViolations.AddRange(ShiftManagerMixGuard.GetViolations(solution, constraints));
            freshViolations.AddRange(ShiftCoverageGuard.GetOverCapacityViolations(solution, constraints));
            freshViolations.AddRange(ShiftCoverageGuard.GetUnderCapacityViolations(solution, constraints));
            freshViolations.AddRange(ApprovedRequestGuard.GetUnmetViolations(solution, constraints));
            freshViolations.AddRange(ShiftEligibilityGuard.GetViolations(solution, constraints));
            freshViolations.AddRange(AdjacentShiftRestGuard.GetViolations(solution, constraints));
            freshViolations.AddRange(DailyDuplicateAssignmentGuard.GetViolations(solution, constraints));
            freshViolations.AddRange(MaxConsecutiveWorkdayRules.GetViolations(solution, constraints));

            solution.Violations = freshViolations;

            var result = new ShiftSchedulingResultDto
            {
                FinalScore = solution.Score,
                Violations = solution.Violations,
                AlgorithmStatus = freshViolations.Any(v => v.Contains("ظرفیت تکمیل نشده") || v.Contains("Under capacity")) ? "Failed" : "Completed"
            };

            // تبدیل انتساب‌ها
            foreach (var assignment in solution.Assignments.Values)
            {
                var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
                var shift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == assignment.ShiftId);

                result.Assignments.Add(new ShiftAssignmentDto
                {
                    UserId = assignment.UserId,
                    UserName = user?.UserName ?? "",
                    ShiftId = assignment.ShiftId,
                    ShiftLabel = assignment.ShiftLabel,
                    Date = assignment.Date,
                    IsOnCall = assignment.IsOnCall,
                    SpecialtyId = user?.SpecialtyId ?? 0,
                    SpecialtyName = user?.SpecialtyName ?? ""
                });
            }

            // محاسبه آمارها
            result.Statistics = new ShiftSchedulingStatisticsDto
            {
                TotalShifts = result.Assignments.Count,
                TotalUsers = constraints.UserConstraints.Count,
                SatisfiedConstraints = constraints.UserConstraints.Count - solution.Violations.Count,
                ViolatedConstraints = solution.Violations.Count,
                AverageShiftsPerUser = constraints.UserConstraints.Count > 0 ?
                    (double)result.Assignments.Count / constraints.UserConstraints.Count : 0
            };

            // آمار شیفت‌ها بر اساس نوع
            result.Statistics.ShiftsByType = result.Assignments
                .GroupBy(a => a.ShiftLabel)
                .ToDictionary(g => g.Key, g => g.Count());

            // آمار شیفت‌ها بر اساس کاربر
            result.Statistics.ShiftsByUser = result.Assignments
                .GroupBy(a => a.UserId)
                .ToDictionary(g => g.Key, g => g.Count());

            PopulateProductivityStatistics(result, constraints);
            return result;
        }

        /// <summary>
        /// تبدیل محدودیت‌ها به فرمت OR-Tools
        /// </summary>
        private async Task<OrToolsConstraints> ConvertToOrToolsConstraintsAsync(ShiftConstraints constraints, ShiftSchedulingRequestDto request)
        {
            var ortoolsConstraints = new OrToolsConstraints
            {
                DepartmentId = constraints.DepartmentId,
                StartDate = constraints.StartDate,
                EndDate = constraints.EndDate,
                GlobalConstraints = new OrToolsGlobalConstraints
                {
                    AllowConsecutiveNightShifts = constraints.GlobalConstraints.AllowConsecutiveNightShifts,
                    MaxConsecutiveNightShifts = constraints.GlobalConstraints.MaxConsecutiveNightShifts,
                    RequireGenderBalance = constraints.GlobalConstraints.RequireGenderBalance,
                    MinGenderBalanceRatio = constraints.GlobalConstraints.MinGenderBalanceRatio,
                    PreferSpecialtyMatch = constraints.GlobalConstraints.PreferSpecialtyMatch,
                    MaxShiftsPerDay = constraints.GlobalConstraints.MaxShiftsPerDay,
                    AllowWeekendShifts = constraints.GlobalConstraints.AllowWeekendShifts,
                    RequireShiftManager = constraints.GlobalConstraints.RequireShiftManager
                },
                HardRules = new OrToolsHardRules
                {
                    ForbidDuplicateDailyAssignments = constraints.HardRules.ForbidDuplicateDailyAssignments,
                    EnforceMaxShiftsPerDay = constraints.HardRules.EnforceMaxShiftsPerDay,
                    EnforceMinRestDays = constraints.HardRules.EnforceMinRestDays,
                    EnforceMaxConsecutiveShifts = constraints.HardRules.EnforceMaxConsecutiveShifts,
                    EnforceWeeklyMaxShifts = constraints.HardRules.EnforceWeeklyMaxShifts,
                    EnforceNightShiftMonthlyCap = constraints.HardRules.EnforceNightShiftMonthlyCap,
                    EnforceSpecialtyCapacity = constraints.HardRules.EnforceSpecialtyCapacity,
                    EnforceProductivityHours = constraints.HardRules.EnforceProductivityHours,
                    AllowEveningAfterNightShift = constraints.HardRules.AllowEveningAfterNightShift,
                    AllowNightShiftAfterNightShift = constraints.HardRules.AllowNightShiftAfterNightShift
                },
                SoftWeights = new OrToolsSoftWeights
                {
                    GenderBalanceWeight = constraints.SoftWeights.GenderBalanceWeight,
                    SpecialtyPreferenceWeight = constraints.SoftWeights.SpecialtyPreferenceWeight,
                    UserUnwantedShiftWeight = constraints.SoftWeights.UserUnwantedShiftWeight,
                    UserPreferredShiftWeight = constraints.SoftWeights.UserPreferredShiftWeight,
                    WeeklyMaxWeight = constraints.SoftWeights.WeeklyMaxWeight,
                    MonthlyNightCapWeight = constraints.SoftWeights.MonthlyNightCapWeight
                },
                HolidayDates = new HashSet<DateTime>(constraints.HolidayDates)
            };

            // تبدیل محدودیت‌های کاربران
            for (int i = 0; i < constraints.UserConstraints.Count; i++)
            {
                var user = constraints.UserConstraints[i];
                var ortoolsUser = new OrToolsUserConstraint
                {
                    UserId = user.UserId,
                    UserIndex = i,
                    UserName = user.UserName,
                    Gender = user.Gender,
                    GenderIndex = user.Gender == UserGender.Male ? 0 : 1,
                    SpecialtyId = user.SpecialtyId,
                    SpecialtyName = user.SpecialtyName,
                    UnavailableDateIndices = user.UnavailableDates.Select(d => (int)(d - constraints.StartDate).TotalDays).ToList(),
                    PreferredShifts = user.PreferredShifts,
                    UnwantedShifts = user.UnwantedShifts,
                    MaxConsecutiveShifts = user.MaxConsecutiveShifts,
                    MinRestDaysBetweenShifts = user.MinRestDaysBetweenShifts,
                    MaxShiftsPerWeek = user.MaxShiftsPerWeek,
                    MaxNightShiftsPerMonth = user.MaxNightShiftsPerMonth,
                    ExactNightShiftCount = user.ExactNightShiftCount,
                    ExactHolidayWeekendNightShiftCount = user.ExactHolidayWeekendNightShiftCount,
                    MinDaysBetweenNightShifts = user.MinDaysBetweenNightShifts,
                    CanBeShiftManager = user.CanBeShiftManager,
                    ShiftManagerLevel = user.ShiftManagerLevel,
                    ShiftType = user.ShiftType,
                    ShiftSubType = user.ShiftSubType,
                    TwoShiftRotationPattern = user.TwoShiftRotationPattern,
                    ProductivityRequiredHours = user.ProductivityRequiredHours.HasValue ? (double)user.ProductivityRequiredHours.Value : null,
                    OvertimeConsent = user.OvertimeConsent,
                    MaxMonthlyOvertimeHours = user.MaxMonthlyOvertimeHours,
                    MaxConsecutiveWorkHours = user.MaxConsecutiveWorkHours
                };

                ortoolsConstraints.UserConstraints.Add(ortoolsUser);
                ortoolsConstraints.UserIndexMap[user.UserId.ToString()] = i;
            }

            // تبدیل نیازمندی‌های شیفت
            for (int i = 0; i < constraints.ShiftRequirements.Count; i++)
            {
                var shift = constraints.ShiftRequirements[i];
                var ortoolsShift = new OrToolsShiftRequirement
                {
                    ShiftId = shift.ShiftId,
                    ShiftIndex = i,
                    ShiftLabel = shift.ShiftLabel,
                    DepartmentId = shift.DepartmentId,
                    StartTime = shift.StartTime,
                    EndTime = shift.EndTime,
                    DurationMinutes = shift.DurationMinutes
                };

                foreach (var specialtyReq in shift.SpecialtyRequirements)
                {
                    var ortoolsSpecialtyReq = new OrToolsSpecialtyRequirement
                    {
                        SpecialtyId = specialtyReq.SpecialtyId,
                        SpecialtyName = specialtyReq.SpecialtyName,
                        RequiredMaleCount = specialtyReq.RequiredMaleCount,
                        RequiredFemaleCount = specialtyReq.RequiredFemaleCount,
                        RequiredTotalCount = specialtyReq.RequiredTotalCount,
                        OnCallMaleCount = specialtyReq.OnCallMaleCount,
                        OnCallFemaleCount = specialtyReq.OnCallFemaleCount,
                        OnCallTotalCount = specialtyReq.OnCallTotalCount,
                        HolidayRequiredMaleCount = specialtyReq.HolidayRequiredMaleCount,
                        HolidayRequiredFemaleCount = specialtyReq.HolidayRequiredFemaleCount,
                        HolidayRequiredTotalCount = specialtyReq.HolidayRequiredTotalCount,
                        HolidayOnCallMaleCount = specialtyReq.HolidayOnCallMaleCount,
                        HolidayOnCallFemaleCount = specialtyReq.HolidayOnCallFemaleCount,
                        HolidayOnCallTotalCount = specialtyReq.HolidayOnCallTotalCount
                    };

                    ortoolsShift.SpecialtyRequirements.Add(ortoolsSpecialtyReq);
                }

                ortoolsConstraints.ShiftRequirements.Add(ortoolsShift);
                ortoolsConstraints.ShiftIndexMap[shift.ShiftId.ToString()] = i;
            }

            return ortoolsConstraints;
        }

        /// <summary>
        /// تبدیل راه‌حل OR-Tools به DTO نتیجه
        /// </summary>
        private async Task<ShiftSchedulingResultDto> ConvertOrToolsSolutionToResultAsync(OrToolsShiftSolution solution, OrToolsConstraints constraints)
        {
            var result = new ShiftSchedulingResultDto
            {
                FinalScore = solution.CalculateScore(),
                Violations = solution.Violations
            };

            // تبدیل انتساب‌ها
            foreach (var assignment in solution.Assignments.Values)
            {
                var user = constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
                var shift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == assignment.ShiftId);

                result.Assignments.Add(new ShiftAssignmentDto
                {
                    UserId = assignment.UserId,
                    UserName = user?.UserName ?? "",
                    ShiftId = assignment.ShiftId,
                    ShiftLabel = assignment.ShiftLabel,
                    Date = assignment.Date,
                    IsOnCall = assignment.IsOnCall,
                    SpecialtyId = user?.SpecialtyId ?? 0,
                    SpecialtyName = user?.SpecialtyName ?? ""
                });
            }

            // محاسبه آمارها
            result.Statistics = new ShiftSchedulingStatisticsDto
            {
                TotalShifts = result.Assignments.Count,
                TotalUsers = constraints.UserConstraints.Count,
                SatisfiedConstraints = constraints.UserConstraints.Count - solution.Violations.Count,
                ViolatedConstraints = solution.Violations.Count,
                AverageShiftsPerUser = constraints.UserConstraints.Count > 0 ?
                    (double)result.Assignments.Count / constraints.UserConstraints.Count : 0
            };

            // آمار شیفت‌ها بر اساس نوع
            result.Statistics.ShiftsByType = result.Assignments
                .GroupBy(a => a.ShiftLabel)
                .ToDictionary(g => g.Key, g => g.Count());

            // آمار شیفت‌ها بر اساس کاربر
            result.Statistics.ShiftsByUser = result.Assignments
                .GroupBy(a => a.UserId)
                .ToDictionary(g => g.Key, g => g.Count());

            return result;
        }

        /// <summary>
        /// تبدیل راه‌حل ترکیبی به DTO نتیجه
        /// </summary>
        private async Task<ShiftSchedulingResultDto> ConvertHybridSolutionToResultAsync(HybridSolution solution, ShiftConstraints constraints)
        {
            var saSolution = ExtractShiftSolution(solution.FinalSolution);
            ApplyMandatoryConstraints(saSolution, constraints);

            var result = await ConvertSolutionToResultAsync(saSolution, constraints);
            result.AlgorithmUsed = SchedulingAlgorithm.Hybrid;

            // اضافه کردن اطلاعات ترکیبی
            result.HybridResult = new HybridResultDto
            {
                StrategyUsed = solution.StrategyUsed,
                TotalExecutionTime = solution.TotalExecutionTime,
                Phase1ExecutionTime = solution.Phase1ExecutionTime,
                Phase2ExecutionTime = solution.Phase2ExecutionTime,
                ParallelExecutionTime = solution.ParallelExecutionTime,
                IterativeExecutionTime = solution.IterativeExecutionTime,
                FallbackExecutionTime = solution.FallbackExecutionTime,
                Errors = solution.Errors
            };

            return result;
        }

        private static ShiftSolution ExtractShiftSolution(object? finalSolution)
        {
            return finalSolution switch
            {
                ShiftSolution sa => sa.Clone(),
                OrToolsShiftSolution ortools => ConvertOrToolsToShiftSolution(ortools),
                _ => new ShiftSolution()
            };
        }

        private static ShiftSolution ConvertOrToolsToShiftSolution(OrToolsShiftSolution ortoolsSolution)
        {
            var saSolution = new ShiftSolution();
            foreach (var assignment in ortoolsSolution.Assignments.Values)
            {
                saSolution.AddAssignment(
                    assignment.UserId,
                    assignment.ShiftId,
                    assignment.Date,
                    assignment.ShiftLabel,
                    assignment.IsOnCall);
            }

            return saSolution;
        }

        private void ApplyMandatoryConstraints(ShiftSolution solution, ShiftConstraints constraints)
        {
            var scheduler = new SimulatedAnnealingScheduler(constraints, new SimulatedAnnealingParameters());
            scheduler.ApplyMandatoryConstraints(solution);
            if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
            {
                scheduler.EnforceMandatoryNightQuotasUntilSatisfied(solution);
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
                ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
                ExactNightQuotaGuard.Enforce(solution, constraints);
            }

            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);

            scheduler.PerformFinalManagerMixRepairSweep(solution);

            if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
            {
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
                ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
                ExactNightQuotaGuard.Enforce(solution, constraints);
            }

            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            MorningEveningBalanceGuard.Enforce(solution, constraints);
            OvertimeBalanceGuard.Enforce(solution, constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
            MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
            ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
            scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
            MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
            ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);
            ShiftEligibilityGuard.StripIneligibleAssignments(solution, constraints);
            scheduler.RefreshSolutionViolations(solution);

            EnsureApprovedRequestsOrThrow(scheduler, solution, constraints);
            EnsureExactNightQuotasOrThrow(scheduler, solution);
            EnsureExactDayShiftQuotasOrThrow(scheduler, solution);
            EnsureHardDailyRulesOrThrow(solution, constraints);
            EnsureMaxConsecutiveWorkdaysOrThrow(solution, constraints);
            ShiftManagerMixGuard.EnsureOrThrow(solution, constraints);
            EnsureSpecialtyCapacityNotExceededOrThrow(solution, constraints);
            EnsureAllShiftCoverageSatisfiedOrThrow(solution, constraints);
        }



        #endregion

        #region Internal Methods (for Persian date conversion)

        /// <summary>
        /// بارگذاری محدودیت‌ها از دیتابیس (نسخه داخلی)
        /// </summary>
        private async Task<ShiftConstraints> LoadConstraintsInternalAsync(ShiftSchedulingRequestInternalDto request)
        {
            // تاریخ‌ها را با .Date نرمال و به‌صورت Unspecified تبدیل می‌کنیم تا round-trip شمسی/میلادی روز را جابه‌جا نکند
            var start = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Unspecified);
            var end = DateTime.SpecifyKind(request.EndDate.Date, DateTimeKind.Unspecified);

            var dto = new ShiftSchedulingRequestDto
            {
                DepartmentId = request.DepartmentId,
                StartDate = ToPersianDateString(start),
                EndDate = ToPersianDateString(end),
                Algorithm = request.Algorithm
            };

            return await LoadConstraintsAsync(dto);
        }

        /// <summary>
        /// بهینه‌سازی با الگوریتم Simulated Annealing (نسخه داخلی)
        /// </summary>
        private async Task<ShiftSchedulingResultDto> OptimizeWithSimulatedAnnealingInternalAsync(
            ShiftSchedulingRequestInternalDto request, 
            ShiftConstraints constraints,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[Phase 1/4 Initialization] Department {DepartmentId}: Loading SA parameters & pre-validating constraints.", request.DepartmentId);
            var saParamsFromDb = await GetAlgorithmSettingsAsync(request.DepartmentId, SchedulingAlgorithm.SimulatedAnnealing, request.AllowExtendedSolverTime);
            var parameters = new SimulatedAnnealingParameters
            {
                InitialTemperature = saParamsFromDb.InitialTemperature,
                FinalTemperature = saParamsFromDb.FinalTemperature,
                CoolingRate = saParamsFromDb.CoolingRate,
                MaxIterations = saParamsFromDb.MaxIterations,
                MaxIterationsWithoutImprovement = saParamsFromDb.MaxIterationsWithoutImprovement
            };

            var scheduler = new SimulatedAnnealingScheduler(constraints, parameters);
            EnsureNightQuotaRequestsFeasibleOrThrow(constraints);
            EnsureConflictingApprovedRequestsOrThrow(constraints);

            var maxAllowedTime = request.AllowExtendedSolverTime
                ? TimeSpan.FromMinutes(6)
                : TimeSpan.FromMinutes(3);

            _logger.LogInformation("[Phase 2/4 Optimization] Department {DepartmentId}: Invoking SA Annealing loop with {MaxIterations} max iterations.", request.DepartmentId, parameters.MaxIterations);

            var solution = await RunCpuBoundWithTimeoutAsync(
                () => scheduler.Optimize(cancellationToken), 
                maxAllowedTime, 
                cancellationToken);

            var statistics = scheduler.GetStatistics();
            _logger.LogInformation("[Phase 2/4 Done] Department {DepartmentId}: SA completed in {Elapsed:0.##}s — {Iterations} iterations, score={Score:0.##}.",
                request.DepartmentId, statistics.ExecutionTime.TotalSeconds, statistics.TotalIterations, statistics.BestScore);

            _logger.LogInformation("[Phase 3/4 Post-Validation Sweeps] Department {DepartmentId}: Executing mandatory constraint checks.", request.DepartmentId);

            // Optimize() already runs ApplyMandatoryConstraints once at the end.
            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: Repairing night quotas and manager mix...", request.DepartmentId);
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);

            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: PerformFinalManagerMixRepairSweep...", request.DepartmentId);
            scheduler.PerformFinalManagerMixRepairSweep(solution);

            if (!scheduler.AreExactNightQuotasSatisfied(solution, out _))
            {
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, constraints);
                ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, constraints);
                ExactNightQuotaGuard.Enforce(solution, constraints);
            }

            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: StripExcessCoverage & Post-Processing...", request.DepartmentId);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            MorningEveningBalanceGuard.Enforce(solution, constraints);
            OvertimeBalanceGuard.Enforce(solution, constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
            MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
            ShiftCoverageGuard.FillRemainingAfterForceApply(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            ExactNightQuotaGuard.Enforce(solution, constraints);
            scheduler.PerformFinalManagerMixRepairSweep(solution, throwIfUnsatisfied: false);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, constraints);
            MaxConsecutiveWorkdayGuard.Enforce(solution, constraints);
            ShiftCoverageGuard.ForceFillAllMissingCoverage(solution, constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, constraints);
            ShiftEligibilityGuard.StripIneligibleAssignments(solution, constraints);
            scheduler.RefreshSolutionViolations(solution);

            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: EnsureApprovedRequests...", request.DepartmentId);
            EnsureApprovedRequestsOrThrow(scheduler, solution, constraints);
            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: EnsureExactNightQuotas...", request.DepartmentId);
            EnsureExactNightQuotasOrThrow(scheduler, solution);
            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: EnsureExactDayShiftQuotas...", request.DepartmentId);
            EnsureExactDayShiftQuotasOrThrow(scheduler, solution);
            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: EnsureHardDailyRules...", request.DepartmentId);
            EnsureHardDailyRulesOrThrow(solution, constraints);
            EnsureMaxConsecutiveWorkdaysOrThrow(solution, constraints);
            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: ShiftManagerMixGuard.EnsureOrThrow...", request.DepartmentId);
            ShiftManagerMixGuard.EnsureOrThrow(solution, constraints);
            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: EnsureSpecialtyCapacityNotExceeded...", request.DepartmentId);
            EnsureSpecialtyCapacityNotExceededOrThrow(solution, constraints);
            _logger.LogInformation("[Phase 3/4] Department {DepartmentId}: EnsureAllShiftCoverageSatisfied...", request.DepartmentId);
            EnsureAllShiftCoverageSatisfiedOrThrow(solution, constraints);

            _logger.LogInformation("[Phase 4/4 Result Conversion] Department {DepartmentId}: Converting solution to result DTO.", request.DepartmentId);
            var result = await ConvertSolutionToResultAsync(solution, constraints);
            result.AlgorithmUsed = SchedulingAlgorithm.SimulatedAnnealing;
            result.AlgorithmStatus = "Completed";
            result.TotalIterations = statistics.TotalIterations;
            result.ExecutionTime = statistics.ExecutionTime;

            return result;
        }

        /// <summary>
        /// بهینه‌سازی با الگوریتم OR-Tools CP-SAT (نسخه داخلی)
        /// </summary>
        private async Task<ShiftSchedulingResultDto> OptimizeWithOrToolsInternalAsync(
            ShiftSchedulingRequestInternalDto request, 
            ShiftConstraints constraints,
            CancellationToken cancellationToken = default)
        {
            // تبدیل محدودیت‌ها به فرمت OR-Tools
            var ortoolsConstraints = await ConvertToOrToolsConstraintsInternalAsync(constraints, request);

            var ortParamsFromDb = await GetOrToolsSettingsAsync(request.DepartmentId, request.AllowExtendedSolverTime);
            var parameters = new OrToolsParameters
            {
                MaxTimeInSeconds = ortParamsFromDb.MaxTimeInSeconds,
                NumSearchWorkers = ortParamsFromDb.NumSearchWorkers,
                LogSearchProgress = ortParamsFromDb.LogSearchProgress,
                MaxSolutions = ortParamsFromDb.MaxSolutions,
                RelativeGapLimit = ortParamsFromDb.RelativeGapLimit
            };

            var scheduler = new OrToolsCPSatScheduler(ortoolsConstraints, parameters);
            var solveTimeout = request.AllowExtendedSolverTime
                ? TimeSpan.FromMinutes(4)
                : TimeSpan.FromSeconds(MaxOrToolsSolveSeconds + 30);

            var solution = await RunCpuBoundWithTimeoutAsync(
                () => scheduler.Optimize(), 
                solveTimeout, 
                cancellationToken);

            var saSolution = ConvertOrToolsToShiftSolution(solution);
            ApplyMandatoryConstraints(saSolution, constraints);

            var result = await ConvertSolutionToResultAsync(saSolution, constraints);
            result.AlgorithmUsed = SchedulingAlgorithm.OrToolsCPSat;
            result.AlgorithmStatus = solution.Status.ToString();
            result.ExecutionTime = solution.SolveTime;

            return result;
        }

        /// <summary>
        /// بهینه‌سازی با الگوریتم ترکیبی (نسخه داخلی)
        /// </summary>
        private async Task<ShiftSchedulingResultDto> OptimizeWithHybridInternalAsync(
            ShiftSchedulingRequestInternalDto request, 
            ShiftConstraints constraints,
            CancellationToken cancellationToken = default)
        {
            // تبدیل محدودیت‌ها به فرمت OR-Tools
            var ortoolsConstraints = await ConvertToOrToolsConstraintsInternalAsync(constraints, request);

            var saParamsFromDb = await GetAlgorithmSettingsAsync(request.DepartmentId, SchedulingAlgorithm.SimulatedAnnealing, request.AllowExtendedSolverTime);
            var saParameters = new SimulatedAnnealingParameters
            {
                InitialTemperature = saParamsFromDb.InitialTemperature,
                FinalTemperature = saParamsFromDb.FinalTemperature,
                CoolingRate = saParamsFromDb.CoolingRate,
                MaxIterations = saParamsFromDb.MaxIterations,
                MaxIterationsWithoutImprovement = saParamsFromDb.MaxIterationsWithoutImprovement
            };

            var ortParamsFromDb = await GetOrToolsSettingsAsync(request.DepartmentId, request.AllowExtendedSolverTime);
            var ortoolsParameters = new OrToolsParameters
            {
                MaxTimeInSeconds = ortParamsFromDb.MaxTimeInSeconds,
                NumSearchWorkers = ortParamsFromDb.NumSearchWorkers,
                LogSearchProgress = ortParamsFromDb.LogSearchProgress,
                MaxSolutions = ortParamsFromDb.MaxSolutions,
                RelativeGapLimit = ortParamsFromDb.RelativeGapLimit
            };

            var hyParamsFromDb = await GetHybridSettingsAsync(request.DepartmentId, request.AllowExtendedSolverTime);
            var hybridParameters = new HybridParameters
            {
                Strategy = hyParamsFromDb.Strategy,
                MaxIterations = hyParamsFromDb.MaxIterations,
                ComplexityThreshold = hyParamsFromDb.ComplexityThreshold
            };

            var scheduler = new HybridScheduler(constraints, ortoolsConstraints, saParameters, ortoolsParameters, hybridParameters);
            var optimizeTimeout = request.AllowExtendedSolverTime
                ? TimeSpan.FromMinutes(12)
                : TimeSpan.FromMinutes(2);

            var solution = await RunCpuBoundWithTimeoutAsync(
                () => scheduler.Optimize(), 
                optimizeTimeout, 
                cancellationToken);
            var statistics = scheduler.GetStatistics();

            var result = await ConvertHybridSolutionToResultAsync(solution, constraints);
            result.AlgorithmUsed = SchedulingAlgorithm.Hybrid;
            result.AlgorithmStatus = "Completed";

            return result;
        }

        /// <summary>
        /// تبدیل محدودیت‌ها به فرمت OR-Tools (نسخه داخلی)
        /// </summary>
        private async Task<OrToolsConstraints> ConvertToOrToolsConstraintsInternalAsync(ShiftConstraints constraints, ShiftSchedulingRequestInternalDto request)
        {
            var dto = new ShiftSchedulingRequestDto
            {
                DepartmentId = request.DepartmentId,
                StartDate = ToPersianDateString(request.StartDate),
                EndDate = ToPersianDateString(request.EndDate),
                Algorithm = request.Algorithm
            };

            return await ConvertToOrToolsConstraintsAsync(constraints, dto);
        }

        #endregion
    }
}
