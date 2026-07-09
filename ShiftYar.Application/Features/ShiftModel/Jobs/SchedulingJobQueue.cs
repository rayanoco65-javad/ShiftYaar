using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftModel.Jobs
{
    /// <summary>
    /// صف درون‌حافظه‌ای مبتنی بر System.Threading.Channels (بدون وابستگی خارجی).
    /// یک مصرف‌کننده (BackgroundService) کارها را یکی‌یکی پردازش می‌کند تا از فشار
    /// همزمان روی CPU/حافظه (که منجر به OOM/502 می‌شد) جلوگیری شود.
    /// </summary>
    public class SchedulingJobQueue : ISchedulingJobQueue
    {
        private readonly Channel<string> _channel;

        public SchedulingJobQueue()
        {
            // نامحدود اما با یک خواننده؛ ترتیب FIFO حفظ می‌شود.
            var options = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            };
            _channel = Channel.CreateUnbounded<string>(options);
        }

        public ValueTask EnqueueAsync(string jobId, CancellationToken cancellationToken = default)
        {
            return _channel.Writer.WriteAsync(jobId, cancellationToken);
        }

        public ValueTask<string> DequeueAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
