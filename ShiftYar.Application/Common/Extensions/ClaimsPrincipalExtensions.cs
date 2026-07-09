using System.Security.Claims;

namespace ShiftYar.Application.Common.Extensions
{
    /// <summary>
    /// Extension methods for ClaimsPrincipal to work with JWT claim types
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        private const string Sub = "sub";
        private const string Name = "name";
        private const string Role = "role";
        private const string Permission = "permission";

        /// <summary>
        /// دریافت شناسه کاربر از claims
        /// </summary>
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirst(Sub)?.Value;
        }

        /// <summary>
        /// دریافت شناسه کاربر به صورت عددی از claims
        /// </summary>
        public static int GetUserIdAsInt(this ClaimsPrincipal user)
        {
            var userId = user.GetUserId();
            return userId != null && int.TryParse(userId, out var id) ? id : 0;
        }

        /// <summary>
        /// دریافت نام کاربر از claims
        /// </summary>
        public static string? GetUserName(this ClaimsPrincipal user)
        {
            return user.FindFirst(Name)?.Value;
        }

        /// <summary>
        /// دریافت نقش‌های کاربر از claims
        /// </summary>
        public static List<string> GetUserRoles(this ClaimsPrincipal user)
        {
            return user.FindAll(Role)
                .Select(c => c.Value)
                .ToList();
        }

        /// <summary>
        /// دریافت مجوزهای کاربر از claims
        /// </summary>
        public static List<string> GetUserPermissions(this ClaimsPrincipal user)
        {
            return user.FindAll(Permission)
                .Select(c => c.Value)
                .ToList();
        }
    }
}
