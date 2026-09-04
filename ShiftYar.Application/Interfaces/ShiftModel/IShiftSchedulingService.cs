using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.Features.ShiftModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.ShiftModel
{
    /// <summary>
    /// اینترفیس سرویس بهینه‌سازی شیفت‌بندی با الگوریتم Simulated Annealing
    /// </summary>
    public interface IShiftSchedulingService
    {
        /// بارگذاری محدودیت‌ها از دیتابیس
        Task<ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models.ShiftConstraints> LoadConstraintsAsync(ShiftSchedulingRequestDto request);

        /// اجرای الگوریتم بهینه‌سازی شیفت‌بندی
        Task<ApiResponse<ShiftSchedulingResultDto>> OptimizeShiftScheduleAsync(ShiftSchedulingRequestDto request, CancellationToken cancellationToken = default);


        /// اجرای الگوریتم بهینه‌سازی شیفت‌بندی (نسخه داخلی با تاریخ میلادی)
        Task<ApiResponse<ShiftSchedulingResultDto>> OptimizeShiftScheduleInternalAsync(ShiftSchedulingRequestInternalDto request, CancellationToken cancellationToken = default);


        /// اجرای کامل فرآیند بهینه‌سازی و ذخیره (اعتبارسنجی + بهینه‌سازی + ذخیره)
        Task<ApiResponse<object>> OptimizeAndSaveAsync(
            ShiftSchedulingRequestDto request,
            bool isBackgroundExecution = false,
            string backgroundJobId = null,
            CancellationToken cancellationToken = default);


        /// دریافت آمارهای الگوریتم
        Task<ApiResponse<object>> GetAlgorithmStatisticsAsync(ShiftSchedulingRequestDto request, CancellationToken cancellationToken = default);


        /// اعتبارسنجی محدودیت‌های شیفت‌بندی
        Task<ApiResponse<List<string>>> ValidateConstraintsAsync(ShiftSchedulingRequestDto request, CancellationToken cancellationToken = default);


        Task<ApiResponse<PagedResponse<ShiftScheduleDtoGet>>> GetFilteredShiftSchedulesAsync(ShiftScheduleFilter filter);

        Task<ApiResponse<ShiftScheduleDtoGet>> GetByIdAsync(int id);


        /// ذخیره نتیجه بهینه‌سازی در دیتابیس
        Task<ApiResponse<string>> SaveOptimizedScheduleAsync(
            ShiftSchedulingResultDto result,
            int? departmentId = null);

        /// حذف شیفت‌بندی ذخیره‌شده یک دپارتمان برای ماه شمسی مشخص (فقط قبل از شروع آن ماه)
        Task<ApiResponse<object>> DeleteMonthlyScheduleAsync(DeleteMonthlyScheduleRequestDto request);

        /// در صورت مسدود بودن شیفت‌بندی ماهانه، متن خطا؛ در غیر این صورت null
        Task<string?> GetMonthlyScheduleCreationBlockerAsync(int departmentId, DateTime rangeStart, DateTime rangeEnd);
    }
}
