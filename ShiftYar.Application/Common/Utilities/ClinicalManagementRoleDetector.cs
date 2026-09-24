using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ShiftYar.Domain.Entities.UserModel;

namespace ShiftYar.Application.Common.Utilities
{
    /// <summary>
    /// تشخیص رده‌های مدیریتی بالینی مشمول ماده ۴ دستورالعمل اجرایی قانون ارتقای بهره‌وری
    /// (سوپروایزرها، سرپرستاران، مترون‌ها و مدیران پرستاری).
    /// </summary>
    public static class ClinicalManagementRoleDetector
    {
        /// <summary>
        /// الگوی تطبیق عناوین پست‌های مدیریتی بالینی به زبان فارسی و انگلیسی (Case-Insensitive).
        /// </summary>
        private static readonly Regex ManagementPattern = new Regex(
            @"(supervisor|superviser|head\s*nurse|headnurse|metron|matron|nursing\s*director|director\s*of\s*nursing|سوپروایزر|سرپرستار|مترون|مدیر\s*پرستاری|مدیر\s*خدمات\s*پرستاری)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// بررسی اینکه آیا فرد دارای پست یا نقش مدیریت بالینی است یا خیر.
        /// </summary>
        public static bool IsClinicalManager(
            string? position = null,
            string? jobTitle = null,
            string? role = null,
            bool? isSupervisor = null,
            bool? isHeadNurse = null,
            params string?[]? additionalTitles)
        {
            if (isSupervisor == true || isHeadNurse == true)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(position) && ManagementPattern.IsMatch(position))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(jobTitle) && ManagementPattern.IsMatch(jobTitle))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(role) && ManagementPattern.IsMatch(role))
            {
                return true;
            }

            if (additionalTitles != null)
            {
                foreach (var title in additionalTitles)
                {
                    if (!string.IsNullOrWhiteSpace(title) && ManagementPattern.IsMatch(title))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// بررسی پست مدیریت بالینی مستقیماً از روی موجودیت User.
        /// فیلدهای بولین، رشته‌های متنی پست/شغل، و همچنین نقش‌های تخصیص‌داده‌شده در UserRoles بررسی می‌شوند.
        /// </summary>
        public static bool IsClinicalManager(User? user)
        {
            if (user == null)
            {
                return false;
            }

            if (user.IsSupervisor == true || user.IsHeadNurse == true)
            {
                return true;
            }

            var titles = new List<string?>();
            if (!string.IsNullOrWhiteSpace(user.Position)) titles.Add(user.Position);
            if (!string.IsNullOrWhiteSpace(user.JobTitle)) titles.Add(user.JobTitle);

            if (user.UserRoles != null)
            {
                foreach (var userRole in user.UserRoles)
                {
                    if (userRole.Role != null && !string.IsNullOrWhiteSpace(userRole.Role.Name))
                    {
                        titles.Add(userRole.Role.Name);
                    }
                }
            }

            if (user.Specialty != null && !string.IsNullOrWhiteSpace(user.Specialty.SpecialtyName))
            {
                titles.Add(user.Specialty.SpecialtyName);
            }

            return titles.Any(t => !string.IsNullOrWhiteSpace(t) && ManagementPattern.IsMatch(t));
        }
    }
}
