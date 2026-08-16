using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Enums.ShiftExchangeModel
{
    public enum ExchangeStatus
    {
        Pending = 0,        // در انتظار تأیید
        Approved = 1,      // تأیید شده
        Rejected = 2,       // رد شده
        Executed = 3,       // اجرا شده
        Cancelled = 4       // لغو شده
    }

    /// <summary>
    /// Swap: تبادل دو شیفت بین دو کاربر.
    /// Transfer: واگذاری شیفت کاربر A به کاربر B که در همان زمان شیفت ندارد؛ A آزاد می‌شود.
    /// </summary>
    public enum ExchangeType
    {
        Swap = 0,
        Transfer = 1
    }
}
