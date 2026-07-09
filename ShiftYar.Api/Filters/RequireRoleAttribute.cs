using Microsoft.AspNetCore.Mvc;

namespace ShiftYar.Api.Filters
{
    ///RequireRoleAttribute : برای کنترل سطح دسترسی بر اساس نقش کاربر
    public class RequireRoleAttribute : TypeFilterAttribute
    {
        public RequireRoleAttribute(string role) : base(typeof(AuthorizeRoleFilter))
        {
            Arguments = new object[] { role };
        }
    }
}
