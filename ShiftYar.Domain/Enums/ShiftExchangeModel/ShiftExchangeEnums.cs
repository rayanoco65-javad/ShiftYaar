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
}
