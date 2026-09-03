using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Api.Filters;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.Features.ShiftModel.Filters;
using ShiftYar.Application.Features.ShiftModel.Jobs;
using ShiftYar.Application.Interfaces.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System.ComponentModel;

namespace ShiftYar.Api.Controllers.ShiftModel
{
    /// <summary>
    /// کنترلر مدیریت بهینه‌سازی شیفت‌بندی با الگوریتم Simulated Annealing
    /// </summary>
    [Authorize]
    public class ShiftSchedulingController : BaseController
    {
        private readonly IShiftSchedulingService _shiftSchedulingService;
        private readonly ISchedulingJobStore _schedulingJobStore;
        private readonly ISchedulingJobQueue _schedulingJobQueue;
        private readonly ILogger<ShiftSchedulingController> _logger;

        public ShiftSchedulingController(
            IShiftSchedulingService shiftSchedulingService,
            ISchedulingJobStore schedulingJobStore,
            ISchedulingJobQueue schedulingJobQueue,
            ILogger<ShiftSchedulingController> logger,
            ShiftYarDbContext context) : base(context)
        {
            _shiftSchedulingService = shiftSchedulingService;
            _schedulingJobStore = schedulingJobStore;
            _schedulingJobQueue = schedulingJobQueue;
            _logger = logger;
            Console.WriteLine($"[CONTROLLER INSTANTIATED] ShiftSchedulingController created at {DateTime.UtcNow:HH:mm:ss.fff}");
            _logger.LogInformation("[CONTROLLER INSTANTIATED] ShiftSchedulingController created.");
        }

        /// <summary>
        /// اکشن تست ایزوله‌شده (Mock Endpoint) جهت تست سلامت شبکه و خط لوله HTTP در زیر ۱ میلی‌ثانیه
        /// </summary>
        [HttpPost("test-mock-optimize")]
        [AllowAnonymous]
        public IActionResult TestMockOptimize([FromBody] ShiftSchedulingRequestDto request)
        {
            return Ok(ApiResponse<object>.Success(new
            {
                Status = "Healthy",
                Message = "خط لوله HTTP و سریال‌سازی پاسخ کاملاً سالم و پاسخ‌گو است.",
                DepartmentId = request.DepartmentId,
                Timestamp = DateTime.UtcNow
            }));
        }

        /// اجرای الگوریتم بهینه‌سازی شیفت‌بندی
        [HttpPost("optimize")]
        [AllowAnonymous]
        public async Task<IActionResult> OptimizeShiftSchedule([FromBody] ShiftSchedulingRequestDto request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[ACTION ENTERED] OptimizeShiftSchedule invoked for DepartmentId={request?.DepartmentId} at {DateTime.UtcNow:HH:mm:ss.fff}");
            _logger.LogInformation("[ACTION ENTERED] OptimizeShiftSchedule invoked for DepartmentId={DepartmentId}", request?.DepartmentId);
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<ShiftSchedulingResultDto>.Fail("Invalid request data"));
                }

                // لایه ایمنی: اضافه کردن تایم‌آوت ۶۰ ثانیه‌ای مستقل برای جلوگیری از معلق ماندن اکشن همزمان
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                // تبدیل تاریخ‌های شمسی به میلادی
                var internalRequest = new ShiftSchedulingRequestInternalDto
                {
                    DepartmentId = request.DepartmentId,
                    StartDate = DateConverter.ConvertToGregorianDate(request.StartDate),
                    EndDate = DateConverter.ConvertToGregorianDate(request.EndDate),
                    Algorithm = request.Algorithm
                };

                var result = await _shiftSchedulingService.OptimizeShiftScheduleInternalAsync(internalRequest, timeoutCts.Token);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(408, ApiResponse<ShiftSchedulingResultDto>.Fail("زمان بهینه‌سازی از سقف مجاز ۱۵ ثانیه فراتر رفت. لطفاً درخواست را به صورت پس‌زمینه اجرا نمایید."));
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, ApiResponse<ShiftSchedulingResultDto>.Fail("درخواست بهینه‌سازی توسط کاربر یا کلاینت لغو شد."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ShiftSchedulingResultDto>.Fail($"Internal server error: {ex.Message}"));
            }
        }

        /// دریافت آمارهای الگوریتم
        [HttpPost("statistics")]
        public async Task<IActionResult> GetAlgorithmStatistics([FromBody] ShiftSchedulingRequestDto request, CancellationToken cancellationToken)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<object>.Fail("Invalid request data"));
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

                // تبدیل تاریخ‌های شمسی به میلادی
                var internalRequest = new ShiftSchedulingRequestInternalDto
                {
                    DepartmentId = request.DepartmentId,
                    StartDate = DateConverter.ConvertToGregorianDate(request.StartDate),
                    EndDate = DateConverter.ConvertToGregorianDate(request.EndDate),
                    Algorithm = request.Algorithm
                };

                var result = await _shiftSchedulingService.GetAlgorithmStatisticsAsync(request, timeoutCts.Token);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
            }
        }

        /// اعتبارسنجی محدودیت‌های شیفت‌بندی
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateConstraints([FromBody] ShiftSchedulingRequestDto request, CancellationToken cancellationToken)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<List<string>>.Fail("Invalid request data"));
                }

                var result = await _shiftSchedulingService.ValidateConstraintsAsync(request, cancellationToken);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<string>>.Fail($"Internal server error: {ex.Message}"));
            }
        }

        /// ذخیره نتیجه بهینه‌سازی در دیتابیس
        /// ذخیره نتیجه شیفت بندی در دیتابیس
        [HttpPost("save")]
        public async Task<IActionResult> SaveOptimizedSchedule([FromBody] ShiftSchedulingResultDto result)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<string>.Fail("Invalid request data"));
                }

                var saveResult = await _shiftSchedulingService.SaveOptimizedScheduleAsync(result);

                if (saveResult.IsSuccess)
                {
                    return Ok(saveResult);
                }
                else
                {
                    return BadRequest(saveResult);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Internal server error: {ex.Message}"));
            }
        }


        ///دریافت شیفت های برنامه ریزی شده
        //[AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<ShiftScheduleDtoGet>>>> GetShiftSchedules([FromQuery] ShiftScheduleFilter filter)
        {
            var result = await _shiftSchedulingService.GetFilteredShiftSchedulesAsync(filter);
            return Ok(result);
        }


        ///دریافت شیفت برنامه ریزی شده
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ShiftScheduleDtoGet>>> GetShiftSchedule(int id)
        {
            var result = await _shiftSchedulingService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                return NotFound(result);
            }

            return Ok(result);
        }

        /// اجرای کامل فرآیند بهینه‌سازی و ذخیره (همزمان)
        /// توجه: برای الگوریتم‌های سنگین (OR-Tools/Hybrid) از نسخهٔ پس‌زمینه استفاده کنید تا از timeout پروکسی (502) جلوگیری شود.
        [HttpPost("optimize-and-save")]
        public async Task<IActionResult> OptimizeAndSave([FromBody] ShiftSchedulingRequestDto request, CancellationToken cancellationToken)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<object>.Fail("Invalid request data"));
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));

                var result = await _shiftSchedulingService.OptimizeAndSaveAsync(request, isBackgroundExecution: false, backgroundJobId: null, cancellationToken: timeoutCts.Token);
                return result.IsSuccess ? Ok(result) : BadRequest(result);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(408, ApiResponse<object>.Fail("زمان بهینه‌سازی و ذخیره از سقف مجاز ۲۰ ثانیه فراتر رفت."));
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, ApiResponse<object>.Fail("درخواست بهینه‌سازی و ذخیره توسط کاربر لغو گردید."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// حذف شیفت‌بندی ذخیره‌شده یک دپارتمان برای ماه شمسی مشخص.
        /// فقط تا قبل از شروع آن ماه مجاز است.
        /// مسیر: POST /DeleteMonthlySchedule
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<object>>> DeleteMonthlySchedule(
            [FromBody] DeleteMonthlyScheduleRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Fail("داده‌های درخواست نامعتبر است."));
            }

            var result = await _shiftSchedulingService.DeleteMonthlyScheduleAsync(request);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// شروع فرآیند بهینه‌سازی به‌صورت پس‌زمینه (فقط بهینه‌سازی، بدون ذخیره نهایی).
        /// بلافاصله یک jobId برمی‌گرداند تا در بک‌گراند اجرا شود و با Polling نتیجه را بگیرید.
        /// </summary>
        [HttpPost("optimize-async")]
        public async Task<IActionResult> EnqueueOptimize([FromBody] ShiftSchedulingRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Fail("Invalid request data"));
            }

            request.SaveAfterOptimize = false;

            var job = await _schedulingJobStore.CreateAsync(request);
            await _schedulingJobQueue.EnqueueAsync(job.Id);

            var payload = new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                statusUrl = $"/api/ShiftScheduling/scheduling-jobs/{job.Id}"
            };

            return Accepted(ApiResponse<object>.Success(payload, "Scheduling job queued. Poll the status URL for the result."));
        }

        /// شروع فرآیند بهینه‌سازی و ذخیره به‌صورت پس‌زمینه.
        /// بلافاصله یک jobId برمی‌گرداند و حل سنگین خارج از درخواست HTTP اجرا می‌شود (بدون 502).
        [HttpPost("optimize-and-save-async")]
        public async Task<IActionResult> EnqueueOptimizeAndSave([FromBody] ShiftSchedulingRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Fail("Invalid request data"));
            }

            request.SaveAfterOptimize = true;

            try
            {
                var start = DateConverter.ConvertToGregorianDate(request.StartDate).Date;
                var end = DateConverter.ConvertToGregorianDate(request.EndDate).Date;
                var blocker = await _shiftSchedulingService.GetMonthlyScheduleCreationBlockerAsync(
                    request.DepartmentId, start, end);
                if (blocker != null)
                {
                    return BadRequest(ApiResponse<object>.Fail(blocker));
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail($"تاریخ نامعتبر است: {ex.Message}"));
            }

            var job = await _schedulingJobStore.CreateAsync(request);
            await _schedulingJobQueue.EnqueueAsync(job.Id);

            var payload = new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                statusUrl = $"/api/ShiftScheduling/scheduling-jobs/{job.Id}"
            };

            return Accepted(ApiResponse<object>.Success(payload, "Scheduling job queued. Poll the status URL for the result."));
        }


        /// دریافت وضعیت/نتیجهٔ یک کار زمان‌بندی پس‌زمینه.
        [HttpGet("scheduling-jobs/{jobId}")]
        public async Task<IActionResult> GetSchedulingJob(string jobId)
        {
            var job = await _schedulingJobStore.GetAsync(jobId);
            if (job == null)
            {
                return NotFound(ApiResponse<object>.Fail("Scheduling job not found."));
            }

            var payload = new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                createdAtUtc = job.CreatedAtUtc,
                startedAtUtc = job.StartedAtUtc,
                completedAtUtc = job.CompletedAtUtc,
                isSuccess = job.IsSuccess,
                message = job.Message,
                result = job.Status == SchedulingJobStatus.Succeeded ? job.Result : null
            };

            return Ok(ApiResponse<object>.Success(payload));
        }
    }
}