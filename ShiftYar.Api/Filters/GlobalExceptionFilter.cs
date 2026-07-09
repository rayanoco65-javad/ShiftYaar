using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ShiftYar.Api.Filters
{
    ///GlobalExceptionFilter: برای هندل کردن همه‌ی خطاهای عمومی و لاگ کردن آنها
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;
        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(context.Exception, "خطای مدیریت نشده رخ داده است.");

            context.Result = new ObjectResult(new
            {
                Success = false,
                Error = context.Exception.Message
            })
            {
                StatusCode = 500
            };

            context.ExceptionHandled = true;
        }
    }
}
