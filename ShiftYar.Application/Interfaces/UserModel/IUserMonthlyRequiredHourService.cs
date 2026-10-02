using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.UserModel
{
    public interface IUserMonthlyRequiredHourService
    {
        /// <summary>
        /// دریافت پیش‌نمایش متمرکز ساعات موظفی پرسنل یک بخش برای یک ماه مشخص (شامل ساعات ذخیره‌شده یا پیشنهادی سیستم)
        /// </summary>
        Task<ApiResponse<DepartmentMonthlyRequiredHoursPreviewDto>> GetDepartmentMonthlyRequiredHoursPreviewAsync(
            int departmentId, int persianYear, int persianMonth);

        /// <summary>
        /// بررسی وضعیت تکمیل ساعات موظفی پرسنل فعال یک بخش برای یک ماه (آیا شیفت‌بندی مجاز است؟)
        /// </summary>
        Task<ApiResponse<DepartmentRequiredHoursStatusDto>> CheckDepartmentRequiredHoursStatusAsync(
            int departmentId, int persianYear, int persianMonth);

        /// <summary>
        /// ذخیره یا به‌روزرسانی گروهی ساعات موظفی پرسنل یک بخش برای یک ماه
        /// </summary>
        Task<ApiResponse<List<UserMonthlyRequiredHourDtoGet>>> BulkUpsertDepartmentRequiredHoursAsync(
            UserMonthlyRequiredHourBulkUpsertDto dto);

        /// <summary>
        /// دریافت لیست ساعات موظفی ذخیره‌شده با فیلتر
        /// </summary>
        Task<ApiResponse<PagedResponse<UserMonthlyRequiredHourDtoGet>>> GetRequiredHoursAsync(
            UserMonthlyRequiredHourFilter filter);

        /// <summary>
        /// دریافت جزئیات یک رکورد با شناسه
        /// </summary>
        Task<ApiResponse<UserMonthlyRequiredHourDtoGet>> GetRequiredHourAsync(int id);

        /// <summary>
        /// دریافت رکورد ساعت موظفی یک کاربر برای سال و ماه مشخص
        /// </summary>
        Task<ApiResponse<UserMonthlyRequiredHourDtoGet>> GetRequiredHourByUserMonthAsync(
            int userId, int persianYear, int persianMonth);
    }
}
