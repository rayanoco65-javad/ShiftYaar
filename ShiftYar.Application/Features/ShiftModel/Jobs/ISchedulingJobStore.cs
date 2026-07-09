using System.Threading.Tasks;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;

namespace ShiftYar.Application.Features.ShiftModel.Jobs
{
    /// <summary>
    /// نگهدارندهٔ پایدار وضعیت کارهای زمان‌بندی پس‌زمینه (روی دیتابیس).
    /// </summary>
    public interface ISchedulingJobStore
    {
        /// ایجاد یک کار جدید در وضعیت Queued و برگرداندن آن
        Task<SchedulingJob> CreateAsync(ShiftSchedulingRequestDto request);

        /// دریافت یک کار بر اساس شناسه (در صورت نبود، null)
        Task<SchedulingJob> GetAsync(string id);

        /// به‌روزرسانی وضعیت/نتیجهٔ یک کار
        Task UpdateAsync(SchedulingJob job);
    }
}
