using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Constants
{
    /// <summary>
    /// Claim types استاندارد JWT برای استفاده در سراسر برنامه
    /// </summary>
    public static class JwtClaimTypes
    {
        /// <summary>
        /// شناسه کاربر (Subject)
        /// </summary>
        public const string Sub = "sub";

        /// <summary>
        /// نام کاربر
        /// </summary>
        public const string Name = "name";

        /// <summary>
        /// نقش کاربر
        /// </summary>
        public const string Role = "role";

        /// <summary>
        /// مجوز کاربر
        /// </summary>
        public const string Permission = "permission";
    }
}
