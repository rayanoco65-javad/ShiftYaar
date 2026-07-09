using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ShiftYar.Api.Filters
{
    ///AuthorizeRoleFilter : برای کنترل سطح دسترسی بر اساس نقش کاربر
    public class AuthorizeRoleFilter : IAuthorizationFilter
    {
        private readonly string _role;
        public AuthorizeRoleFilter(string role)
        {
            _role = role;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (!user.Identity.IsAuthenticated || !user.IsInRole(_role))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
