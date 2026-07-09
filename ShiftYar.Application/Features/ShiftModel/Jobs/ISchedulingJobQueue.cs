using System.Threading;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftModel.Jobs
{
    /// <summary>
    /// صف کارهای زمان‌بندی پس‌زمینه (تولیدکننده/مصرف‌کننده) مبتنی بر Channel بومی .NET.
    /// </summary>
    public interface ISchedulingJobQueue
    {
        /// افزودن شناسهٔ کار به صف
        ValueTask EnqueueAsync(string jobId, CancellationToken cancellationToken = default);

        /// دریافت شناسهٔ کار بعدی از صف (تا زمان موجود شدن منتظر می‌ماند)
        ValueTask<string> DequeueAsync(CancellationToken cancellationToken);
    }
}
