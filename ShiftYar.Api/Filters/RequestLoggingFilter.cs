using Microsoft.AspNetCore.Mvc.Filters;
using ShiftYar.Application.Common.Constants;
using System.Security.Claims;

namespace ShiftYar.Api.Filters
{
    ///RequestLoggingFilter: برای ثبت لاگ هنگام اجرای اکشن‌ها
    public class RequestLoggingFilter : IActionFilter
    {
        private readonly ILogger<RequestLoggingFilter> _logger;

        public RequestLoggingFilter(ILogger<RequestLoggingFilter> logger)
        {
            _logger = logger;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;
            //var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            //var userName = user.FindFirst(ClaimTypes.Name)?.Value ?? "Anonymous";
            //var userRoles = string.Join(", ", user.FindAll(ClaimTypes.Role).Select(c => c.Value));
            var userId = user.FindFirst(JwtClaimTypes.Sub)?.Value ?? "Anonymous";
            var userName = user.FindFirst(JwtClaimTypes.Name)?.Value ?? "Anonymous";
            var userRoles = string.Join(", ", user.FindAll(JwtClaimTypes.Role).Select(c => c.Value));

            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var device = context.HttpContext.Request.Headers["User-Agent"].ToString();
            var action = context.ActionDescriptor.DisplayName;

            _logger.LogInformation(
                "➡ شروع اکشن: {Action} | کاربر: {UserName} (ID: {UserId}) | نقش‌ها: {UserRoles} | IP: {IpAddress} | دستگاه: {Device}",
                action, userName, userId, userRoles, ipAddress, device
            );
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            var user = context.HttpContext.User;
            //var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            //var userName = user.FindFirst(ClaimTypes.Name)?.Value ?? "Anonymous";
            var userId = user.FindFirst(JwtClaimTypes.Sub)?.Value ?? "Anonymous";
            var userName = user.FindFirst(JwtClaimTypes.Name)?.Value ?? "Anonymous";
            var action = context.ActionDescriptor.DisplayName;
            var statusCode = context.HttpContext.Response.StatusCode;

            _logger.LogInformation(
                "⬅ پایان اکشن: {Action} | کاربر: {UserName} (ID: {UserId}) | وضعیت: {StatusCode}",
                action, userName, userId, statusCode
            );
        }
    }
}
