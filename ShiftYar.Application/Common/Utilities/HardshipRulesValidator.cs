using System;

namespace ShiftYar.Application.Common.Utilities
{
    /// <summary>
    /// قوانین اعتبارسنجی فیلدهای سختی کار (درصد و امتیاز).
    /// طبق الزامات دستورالعمل اجرایی، کاربر در هر لحظه صرفاً می‌تواند یکی از دو فیلد «درصد سختی کار» یا «امتیاز سختی کار» را تکمیل کند.
    /// </summary>
    public static class HardshipRulesValidator
    {
        public const string MutuallyExclusiveErrorMessage =
            "امکان ثبت همزمان درصد سختی کار و امتیاز سختی کار وجود ندارد. لطفاً صرفاً یکی از دو فیلد درصد یا امتیاز را وارد نمایید.";

        public const string NegativePercentageErrorMessage =
            "درصد سختی کار نمی‌تواند منفی باشد.";

        public const string ExceededPercentageErrorMessage =
            "درصد سختی کار نمی‌تواند بیش از ۱۰۰ درصد باشد.";

        public const string NegativeScoreErrorMessage =
            "امتیاز سختی کار نمی‌تواند منفی باشد.";

        /// <summary>
        /// اعتبارسنجی شرط انحصاری متقابل (Mutually Exclusive / XOR) و بازه‌های مجاز مقادیر.
        /// </summary>
        /// <param name="hardshipPercent">درصد سختی کار (۰–۱۰۰)</param>
        /// <param name="hardshipScore">امتیاز سختی کار قانون مدیریت خدمات کشوری</param>
        /// <returns>پیام خطا در صورت عدم انطباق با قوانین؛ یا null در صورت معتبر بودن.</returns>
        public static string? Validate(decimal? hardshipPercent, decimal? hardshipScore)
        {
            var hasPercentage = hardshipPercent.HasValue && hardshipPercent.Value > 0m;
            var hasScore = hardshipScore.HasValue && hardshipScore.Value >= 0m;

            if (hasPercentage && hasScore)
            {
                return MutuallyExclusiveErrorMessage;
            }

            if (hardshipPercent.HasValue && hardshipPercent.Value < 0m)
            {
                return NegativePercentageErrorMessage;
            }

            if (hardshipPercent.HasValue && hardshipPercent.Value > 100m)
            {
                return ExceededPercentageErrorMessage;
            }

            if (hardshipScore.HasValue && hardshipScore.Value < 0m)
            {
                return NegativeScoreErrorMessage;
            }

            return null;
        }

        /// <summary>
        /// بررسی سریع اینکه آیا ورود همزمان رخ داده است یا خیر.
        /// </summary>
        public static bool IsMutuallyExclusiveValid(decimal? hardshipPercent, decimal? hardshipScore)
        {
            return Validate(hardshipPercent, hardshipScore) == null;
        }
    }
}
