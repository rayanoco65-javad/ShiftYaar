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
using AutoMapper;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ShiftYar.Application.Features.ShiftModel.Services
{
    /// <summary>
    /// سرویس بهینه‌سازی شیفت‌بندی با الگوریتم Simulated Annealing
    /// </summary>
    public class ShiftSchedulingService : IShiftSchedulingService
    {
        private readonly IEfRepository<User> _userRepository;
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
        public async Task<ApiResponse<ShiftSchedulingResultDto>> OptimizeShiftScheduleAsync(ShiftSchedulingRequestDto request)
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
                        result = await OptimizeWithSimulatedAnnealingAsync(request, constraints);
                        break;
                    case SchedulingAlgorithm.OrToolsCPSat:
                        await ApplyAlgorithmSettingsFromDbAsync(request);
                        result = await OptimizeWithOrToolsAsync(request, constraints);
                        break;
                    case SchedulingAlgorithm.Hybrid:
                        await ApplyAlgorithmSettingsFromDbAsync(request);
                        result = await OptimizeWithHybridAsync(request, constraints);
                        break;
                    default:
                        result = await OptimizeWithSimulatedAnnealingAsync(request, constraints);
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
        public async Task<ApiResponse<ShiftSchedulingResultDto>> OptimizeShiftScheduleInternalAsync(ShiftSchedulingRequestInternalDto request)
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
                        result = await OptimizeWithSimulatedAnnealingInternalAsync(request, constraints);
                        break;
                    case SchedulingAlgorithm.OrToolsCPSat:
                        result = await OptimizeWithOrToolsInternalAsync(request, constraints);
                        break;
                    case SchedulingAlgorithm.Hybrid:
                        result = await OptimizeWithHybridInternalAsync(request, constraints);
                        break;
                    default:
                        result = await OptimizeWithSimulatedAnnealingInternalAsync(request, constraints);
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


        /// اجرای کامل فرآیند بهینه‌سازی و ذخیره (اعتبارسنجی + بهینه‌سازی + ذخیره)
        /// این متد هم توسط اکشن همزمان و هم توسط اجرای پس‌زمینه استفاده می‌شود تا منطق تکرار نشود.
        public async Task<ApiResponse<object>> OptimizeAndSaveAsync(
            ShiftSchedulingRequestDto request,
            bool isBackgroundExecution = false,
            string backgroundJobId = null)
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

                // اعتبارسنجی اولیه
                var validationResult = await ValidateConstraintsAsync(request);
                if (!validationResult.IsSuccess || (validationResult.Data?.Count ?? 0) > 0)
                {
                    return ApiResponse<object>.Fail($"Validation failed: {string.Join(", ", validationResult.Data ?? new List<string>())}");
                }

                await ReportJobProgressAsync(backgroundJobId, "Loading department data and running optimizer...");

                // اجرای بهینه‌سازی
                var optimizationResult = await OptimizeShiftScheduleInternalAsync(internalRequest);
                if (!optimizationResult.IsSuccess)
                {
                    return ApiResponse<object>.Fail(optimizationResult.Message ?? "Optimization failed");
                }

                await ReportJobProgressAsync(backgroundJobId, "Saving optimized schedule...");

                // ذخیره نتیجه
                var saveResult = await SaveOptimizedScheduleAsync(optimizationResult.Data);
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
        public async Task<ApiResponse<object>> GetAlgorithmStatisticsAsync(ShiftSchedulingRequestDto request)
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
                var solution = scheduler.Optimize();
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
        public async Task<ApiResponse<List<string>>> ValidateConstraintsAsync(ShiftSchedulingRequestDto request)
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
        public async Task<ApiResponse<string>> SaveOptimizedScheduleAsync(ShiftSchedulingResultDto result)
        {
            try
            {
                if (result.Assignments == null || result.Assignments.Count == 0)
                {
                    return ApiResponse<string>.Fail("No assignments to save");
                }

                _logger.LogInformation("Saving optimized schedule with {Count} assignments", result.Assignments.Count);

                var startDate = result.Assignments.Min(a => a.Date).Date;
                var endDate = result.Assignments.Max(a => a.Date).Date;
                var scheduleShiftIds = result.Assignments.Select(a => a.ShiftId).Distinct().ToHashSet();

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

                // حذف همه انتساب‌های قبلی همین شیفت‌های برنامه در بازه (نه فقط ۱۰ ردیف اول)
                var (existingAssignments, existingTotal) = await _shiftAssignmentRepository.GetByFilterAsync(
                    filter: new Application.Common.Filters.SimpleFilter<ShiftAssignment>(a =>
                        a.ShiftDateId.HasValue &&
                        a.ShiftDate != null &&
                        a.ShiftDate.Date.HasValue &&
                        a.ShiftDate.Date.Value.Date >= startDate &&
                        a.ShiftDate.Date.Value.Date <= endDate &&
                        a.ShiftId.HasValue &&
                        scheduleShiftIds.Contains(a.ShiftId.Value)
                    ),
                    includes: "ShiftDate"
                );

                _logger.LogInformation(
                    "SaveOptimizedSchedule: Deleting {Loaded}/{Total} existing assignment(s) for shifts [{ShiftIds}] in {Start}..{End}",
                    existingAssignments.Count,
                    existingTotal,
                    string.Join(",", scheduleShiftIds),
                    startDate.ToString("yyyy-MM-dd"),
                    endDate.ToString("yyyy-MM-dd"));

                foreach (var ea in existingAssignments)
                {
                    _shiftAssignmentRepository.Delete(ea);
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

                _logger.LogInformation("Successfully saved {Count} shift assignments", result.Assignments.Count);

                return ApiResponse<string>.Success("Schedule saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while saving optimized schedule");
                return ApiResponse<string>.Fail($"Error: {ex.Message}");
            }
        }


        #region Algorithm-Specific Optimization Methods

        /// <summary>
        /// بهینه‌سازی با الگوریتم Simulated Annealing
        /// </summary>
        private async Task<ShiftSchedulingResultDto> OptimizeWithSimulatedAnnealingAsync(ShiftSchedulingRequestDto request, ShiftConstraints constraints) // اجرای SA با پارامترهای ورودی و داده‌های DB
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
            var solution = scheduler.Optimize();
            var statistics = scheduler.GetStatistics();

            scheduler.ApplyMandatoryConstraints(solution);
            EnsureApprovedRequestsOrThrow(scheduler, solution, constraints);

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
        private async Task<ShiftSchedulingResultDto> OptimizeWithOrToolsAsync(ShiftSchedulingRequestDto request, ShiftConstraints constraints) // اجرای OR-Tools با تبدیل قیود و برگرداندن نتیجه
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
            var solution = scheduler.Optimize();

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
        private async Task<ShiftSchedulingResultDto> OptimizeWithHybridAsync(ShiftSchedulingRequestDto request, ShiftConstraints constraints) // اجرای الگوریتم ترکیبی با استراتژی خواسته‌شده
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
            var solution = scheduler.Optimize();
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

        private static T RunCpuBoundWithTimeout<T>(Func<T> work, TimeSpan timeout)
        {
            var task = Task.Run(work);
            if (!task.Wait(timeout))
            {
                throw new TimeoutException($"Optimizer exceeded time limit of {timeout.TotalMinutes:0.#} minutes.");
            }

            return task.GetAwaiter().GetResult();
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

                    return (
                        settings.SA_InitialTemperature ?? 1000.0,
                        settings.SA_FinalTemperature ?? 0.1,
                        settings.SA_CoolingRate ?? 0.95,
                        maxIterations,
                        maxWithoutImprovement
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در دریافت تنظیمات الگوریتم از دیتابیس، از مقادیر پیش‌فرض استفاده می‌شود");
            }

            // مقادیر پیش‌فرض
            var defaultMaxIterations = forBackground ? MaxBackgroundSaIterations : 10000;
            var defaultMaxWithoutImprovement = forBackground ? 800 : 1000;
            return (1000.0, 0.1, 0.95, defaultMaxIterations, defaultMaxWithoutImprovement);
        }

        private WorkingHoursCalculationResultDto? CalculateProductivitySnapshot(User user, UserConstraint userConstraint, ShiftConstraints constraints, DepartmentSchedulingSettings? deptSetting, double nightShiftDurationHours)
        {
            if (user.IncludedProductivityPlan != true)
            {
                return null;
            }

            var staffInfo = new StaffEmploymentInfoDto
            {
                StaffId = user.Id ?? 0,
                StaffFullName = user.FullName,
                DateOfEmployment = user.DateOfEmployment,
                // Hardship duty must come from a dedicated field or manual override on the request DTO.
                // isProjectPersonnel only indicates residency (طرحی) and must not affect productivity reductions.
                HasHardshipDuty = false,
                HasUncommonRotatingShifts = userConstraint.ShiftType == ShiftTypes.RotatingShift
            };

            var weeks = CalculateWeekSpan(constraints.StartDate, constraints.EndDate);
            var nightCap = userConstraint.MaxNightShiftsPerMonth > 0
                ? userConstraint.MaxNightShiftsPerMonth
                : deptSetting?.MaxNightShiftsPerMonth ?? 0;

            var nightHolidayHours = nightCap > 0 && nightShiftDurationHours > 0
                ? (decimal)(nightCap * nightShiftDurationHours)
                : 0m;

            var request = new WorkingHoursCalculationRequestDto
            {
                Staff = staffInfo,
                TargetMonth = new DateTime(constraints.StartDate.Year, constraints.StartDate.Month, 1),
                NumberOfWeeksInMonth = weeks,
                NightHolidayHours = nightHolidayHours
            };

            try
            {
                return _workingHoursCalculator.CalculateMonthlyHours(request);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate productivity hours for user {UserId}", user.Id);
                return null;
            }
        }

        private static int CalculateWeekSpan(DateTime startDate, DateTime endDate)
        {
            var totalDays = (endDate.Date - startDate.Date).TotalDays + 1;
            if (totalDays <= 0)
            {
                return 1;
            }

            return Math.Max(1, (int)Math.Ceiling(totalDays / 7.0));
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

        private static string ToPersianDateString(DateTime date)
        {
            var calendar = new PersianCalendar();
            return $"{calendar.GetYear(date):0000}/{calendar.GetMonth(date):00}/{calendar.GetDayOfMonth(date):00}";
        }

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

            var shiftDurationMap = constraints.ShiftRequirements
                .GroupBy(s => s.ShiftId)
                .ToDictionary(g => g.Key, g => g.First().DurationHours);

            var hoursByUser = result.Assignments
                .GroupBy(a => a.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(a => shiftDurationMap.TryGetValue(a.ShiftId, out var duration) ? duration : 0));

            result.Statistics ??= new ShiftSchedulingStatisticsDto();
            result.Statistics.WorkedHoursByUser = hoursByUser;
            result.Statistics.TotalScheduledHours = hoursByUser.Values.Sum();

            var requiredByUser = constraints.UserConstraints
                .Where(u => u.ProductivityRequiredHours.HasValue)
                .ToDictionary(u => u.UserId, u => (double)u.ProductivityRequiredHours.Value);

            result.Statistics.ProductivityRequiredHoursByUser = requiredByUser;

            var overtime = new Dictionary<int, double>();
            var compliantCount = 0;
            const double tolerance = 0.25;

            foreach (var kvp in requiredByUser)
            {
                var worked = hoursByUser.TryGetValue(kvp.Key, out var value) ? value : 0;
                var delta = worked - kvp.Value;
                if (delta > tolerance)
                {
                    overtime[kvp.Key] = delta;
                }
                else
                {
                    overtime[kvp.Key] = 0;
                    compliantCount++;
                }
            }

            result.Statistics.ProductivityOvertimeByUser = overtime;
            if (requiredByUser.Count > 0)
            {
                result.Statistics.ProductivityComplianceRate = compliantCount / (double)requiredByUser.Count;
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
        private async Task<ShiftConstraints> LoadConstraintsAsync(ShiftSchedulingRequestDto request) // بارگذاری قیود زمان‌بندی از DB
        {
            try
            {
                var constraints = new ShiftConstraints
                {
                    DepartmentId = request.DepartmentId,
                    StartDate = DateConverter.ConvertToGregorianDate(request.StartDate.Trim()),
                    EndDate = DateConverter.ConvertToGregorianDate(request.EndDate.Trim())
                };

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
                    constraints.HardRules.EnforceSpecialtyCapacity = deptSettingEarly.EnforceSpecialtyCapacity ?? false;

                    // تنظیم Soft Weights
                    constraints.SoftWeights.GenderBalanceWeight = deptSettingEarly.GenderBalanceWeight ?? 1.0;
                    constraints.SoftWeights.SpecialtyPreferenceWeight = deptSettingEarly.SpecialtyPreferenceWeight ?? 1.0;
                    constraints.SoftWeights.UserUnwantedShiftWeight = deptSettingEarly.UserUnwantedShiftWeight ?? 1.0;
                    constraints.SoftWeights.UserPreferredShiftWeight = deptSettingEarly.UserPreferredShiftWeight ?? 1.0;
                    constraints.SoftWeights.WeeklyMaxWeight = deptSettingEarly.WeeklyMaxWeight ?? 1.0;
                    constraints.SoftWeights.MonthlyNightCapWeight = deptSettingEarly.MonthlyNightCapWeight ?? 1.0;
                    constraints.SoftWeights.FairShiftCountBalanceWeight = deptSettingEarly.FairShiftCountBalanceWeight ?? 1.0;
                    constraints.SoftWeights.ExtraShiftRotationWeight = deptSettingEarly.ExtraShiftRotationWeight ?? 1.0;
                    constraints.SoftWeights.ShiftLabelBalanceWeight = deptSettingEarly.ShiftLabelBalanceWeight ?? 1.0;

                    constraints.GlobalConstraints.RequireManagerForEveningShift = deptSettingEarly.RequireManagerForEveningShift ?? false;
                    constraints.GlobalConstraints.RequireManagerForNightShift = deptSettingEarly.RequireManagerForNightShift ?? false;
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

                foreach (var user in departmentUsers)
                {
                    var userConstraint = new UserConstraint
                    {
                        UserId = user.Id ?? 0,
                        UserName = user.FullName ?? "",
                        Gender = user.Gender ?? UserGender.Male,
                        SpecialtyId = user.SpecialtyId ?? 0,
                        SpecialtyName = user.Specialty?.SpecialtyName ?? "",
                        CanBeShiftManager = user.CanBeShiftManager ?? false,
                        IsActive = user.IsActive ?? true,
                        ShiftType = user.ShiftType ?? ShiftTypes.FixedShift,
                        ShiftSubType = user.ShiftSubType ?? ShiftSubTypes.FixedMorning,
                        TwoShiftRotationPattern = user.TwoShiftRotationPattern
                    };

                    // همه محدودیت‌های عددی از تنظیمات دپارتمان خوانده می‌شوند (مقادیر پیش‌فرض)
                    userConstraint.MaxConsecutiveShifts = 3; // پیش‌فرض
                    userConstraint.MinRestDaysBetweenShifts = 1; // پیش‌فرض
                    userConstraint.MaxShiftsPerWeek = 5; // پیش‌فرض
                    userConstraint.MaxNightShiftsPerMonth = 8; // پیش‌فرض

                    // Override from department settings if enforcement is on
                    if (deptSettingEarly != null)
                    {
                        if (constraints.HardRules.EnforceMinRestDays && deptSettingEarly.MinRestDaysBetweenShifts.HasValue)
                        {
                            userConstraint.MinRestDaysBetweenShifts = Math.Max(0, deptSettingEarly.MinRestDaysBetweenShifts.Value);
                        }
                        if (constraints.HardRules.EnforceMaxConsecutiveShifts && deptSettingEarly.MaxConsecutiveShifts.HasValue)
                        {
                            userConstraint.MaxConsecutiveShifts = Math.Max(1, deptSettingEarly.MaxConsecutiveShifts.Value);
                        }
                        if (constraints.HardRules.EnforceWeeklyMaxShifts && deptSettingEarly.MaxShiftsPerWeek.HasValue)
                        {
                            userConstraint.MaxShiftsPerWeek = Math.Clamp(deptSettingEarly.MaxShiftsPerWeek.Value, 1, 7);
                        }
                        if (constraints.HardRules.EnforceNightShiftMonthlyCap && deptSettingEarly.MaxNightShiftsPerMonth.HasValue)
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
                        EndTime = shift.EndTime ?? TimeSpan.Zero
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
                                OnCallTotalCount = reqSpecialty.OnCallTottalCount ?? 0
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
                    if (productivitySnapshot != null)
                    {
                        userConstraint.IncludedInProductivityPlan = true;
                        userConstraint.ProductivitySnapshot = productivitySnapshot;
                        userConstraint.ProductivityRequiredHours = productivitySnapshot.FinalMonthlyRequiredHours;
                    }
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

                            var slot = new ShiftSlotConstraint { Date = date, ShiftLabel = req.ShiftLabel.Value };
                            if (!uc.UnavailableShiftSlots.Any(s => s.Date.Date == slot.Date.Date && s.ShiftLabel == slot.ShiftLabel))
                            {
                                uc.UnavailableShiftSlots.Add(slot);
                                appliedOffSlot++;
                                _logger.LogInformation(
                                    "LoadConstraints: OFF-Slot UserId={UserId} Date={Date:yyyy-MM-dd} Label={Label} RequestId={RequestId}",
                                    uc.UserId, date, slot.ShiftLabel, req.Id);
                            }
                        }
                    }
                    else if (req.RequestAction == Domain.Enums.ShiftRequestModel.RequestAction.RequestToBeOnShift)
                    {
                        if (req.RequestType == Domain.Enums.ShiftRequestModel.RequestType.FullDay)
                        {
                            if (!uc.RequiredPresenceDates.Any(d => d.Date == date))
                            {
                                uc.RequiredPresenceDates.Add(date);
                                appliedOnFull++;
                                _logger.LogInformation(
                                    "LoadConstraints: ON-FullDay UserId={UserId} Date={Date:yyyy-MM-dd} RequestId={RequestId}",
                                    uc.UserId, date, req.Id);
                            }
                        }
                        else
                        {
                            if (!req.ShiftLabel.HasValue)
                            {
                                skippedIncomplete++;
                                _logger.LogWarning(
                                    "LoadConstraints: Skipping SpecificShift ON request {RequestId} — ShiftLabel is null",
                                    req.Id);
                                continue;
                            }

                            var slot = new ShiftSlotConstraint { Date = date, ShiftLabel = req.ShiftLabel.Value };
                            if (!uc.RequiredShiftSlots.Any(s => s.Date.Date == slot.Date.Date && s.ShiftLabel == slot.ShiftLabel))
                            {
                                uc.RequiredShiftSlots.Add(slot);
                                appliedOnSlot++;
                                _logger.LogInformation(
                                    "LoadConstraints: ON-Slot UserId={UserId} Date={Date:yyyy-MM-dd} Label={Label} RequestId={RequestId}",
                                    uc.UserId, date, slot.ShiftLabel, req.Id);
                            }
                        }
                    }
                }

                _logger.LogInformation(
                    "LoadConstraints: Applied approved requests — OffFull={OffFull}, OffSlot={OffSlot}, OnFull={OnFull}, OnSlot={OnSlot}, SkippedIncomplete={Skipped}",
                    appliedOffFull, appliedOffSlot, appliedOnFull, appliedOnSlot, skippedIncomplete);

                if (approvedRequestItems.Count > 0 &&
                    appliedOffFull + appliedOffSlot + appliedOnFull + appliedOnSlot == 0 &&
                    skippedIncomplete == approvedRequestItems.Count)
                {
                    _logger.LogError(
                        "LoadConstraints: {Count} approved request(s) found but NONE could be mapped (all incomplete).",
                        approvedRequestItems.Count);
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

                    // Apply department-level numeric values to global constraints if enforced
                    if (constraints.HardRules.EnforceMaxShiftsPerDay && deptSetting.MaxShiftsPerDay.HasValue)
                    {
                        constraints.GlobalConstraints.MaxShiftsPerDay = Math.Max(1, deptSetting.MaxShiftsPerDay.Value);
                    }
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
                    if (deptSetting.ShiftLabelBalanceWeight.HasValue) constraints.SoftWeights.ShiftLabelBalanceWeight = deptSetting.ShiftLabelBalanceWeight.Value;
                    if (deptSetting.FairnessLookbackMonths.HasValue) constraints.SoftWeights.FairnessLookbackMonths = deptSetting.FairnessLookbackMonths.Value;

                    // Night shift distribution weights
                    if (deptSetting.NightShiftDistributionWeight.HasValue) constraints.SoftWeights.NightShiftDistributionBySeniorityWeight = deptSetting.NightShiftDistributionWeight.Value;
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
            var result = new ShiftSchedulingResultDto
            {
                FinalScore = solution.Score,
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
                    EnforceProductivityHours = constraints.HardRules.EnforceProductivityHours
                },
                SoftWeights = new OrToolsSoftWeights
                {
                    GenderBalanceWeight = constraints.SoftWeights.GenderBalanceWeight,
                    SpecialtyPreferenceWeight = constraints.SoftWeights.SpecialtyPreferenceWeight,
                    UserUnwantedShiftWeight = constraints.SoftWeights.UserUnwantedShiftWeight,
                    UserPreferredShiftWeight = constraints.SoftWeights.UserPreferredShiftWeight,
                    WeeklyMaxWeight = constraints.SoftWeights.WeeklyMaxWeight,
                    MonthlyNightCapWeight = constraints.SoftWeights.MonthlyNightCapWeight
                }
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
                    CanBeShiftManager = user.CanBeShiftManager,
                    ShiftType = user.ShiftType,
                    ShiftSubType = user.ShiftSubType,
                    TwoShiftRotationPattern = user.TwoShiftRotationPattern,
                    ProductivityRequiredHours = user.ProductivityRequiredHours.HasValue ? (double)user.ProductivityRequiredHours.Value : null
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
                        OnCallTotalCount = specialtyReq.OnCallTotalCount
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
            EnsureApprovedRequestsOrThrow(scheduler, solution, constraints);
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
        private async Task<ShiftSchedulingResultDto> OptimizeWithSimulatedAnnealingInternalAsync(ShiftSchedulingRequestInternalDto request, ShiftConstraints constraints)
        {
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
            var solution = scheduler.Optimize();
            var statistics = scheduler.GetStatistics();

            scheduler.ApplyMandatoryConstraints(solution);
            EnsureApprovedRequestsOrThrow(scheduler, solution, constraints);

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
        private async Task<ShiftSchedulingResultDto> OptimizeWithOrToolsInternalAsync(ShiftSchedulingRequestInternalDto request, ShiftConstraints constraints)
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

            var solution = request.AllowExtendedSolverTime
                ? RunCpuBoundWithTimeout(() => scheduler.Optimize(), solveTimeout)
                : scheduler.Optimize();

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
        private async Task<ShiftSchedulingResultDto> OptimizeWithHybridInternalAsync(ShiftSchedulingRequestInternalDto request, ShiftConstraints constraints)
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

            var solution = request.AllowExtendedSolverTime
                ? RunCpuBoundWithTimeout(() => scheduler.Optimize(), optimizeTimeout)
                : scheduler.Optimize();
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
