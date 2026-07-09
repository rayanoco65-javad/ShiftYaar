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
        /// اجرای الگوریتم بهینه‌سازی شیفت‌بندی
        Task<ApiResponse<ShiftSchedulingResultDto>> OptimizeShiftScheduleAsync(ShiftSchedulingRequestDto request);


        /// اجرای الگوریتم بهینه‌سازی شیفت‌بندی (نسخه داخلی با تاریخ میلادی)
        Task<ApiResponse<ShiftSchedulingResultDto>> OptimizeShiftScheduleInternalAsync(ShiftSchedulingRequestInternalDto request);


        /// اجرای کامل فرآیند بهینه‌سازی و ذخیره (اعتبارسنجی + بهینه‌سازی + ذخیره)
        Task<ApiResponse<object>> OptimizeAndSaveAsync(ShiftSchedulingRequestDto request);


        /// دریافت آمارهای الگوریتم
        Task<ApiResponse<object>> GetAlgorithmStatisticsAsync(ShiftSchedulingRequestDto request);


        /// اعتبارسنجی محدودیت‌های شیفت‌بندی
        Task<ApiResponse<List<string>>> ValidateConstraintsAsync(ShiftSchedulingRequestDto request);


        Task<ApiResponse<PagedResponse<ShiftScheduleDtoGet>>> GetFilteredShiftSchedulesAsync(ShiftScheduleFilter filter);

        Task<ApiResponse<ShiftScheduleDtoGet>> GetByIdAsync(int id);


        /// ذخیره نتیجه بهینه‌سازی در دیتابیس
        Task<ApiResponse<string>> SaveOptimizedScheduleAsync(ShiftSchedulingResultDto result);
    }
}
