using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Enums.ShiftRequestModel
{
    public enum RequestType
    {
        /// <summary>تمام روز — فقط برای عدم‌حضور/مرخصی معتبر است؛ حضور کل‌روز مجاز نیست.</summary>
        FullDay = 0,
        /// <summary>یک شیفت مشخص (صبح یا عصر یا شب)</summary>
        SpecificShift = 1
    }

    public enum RequestAction
    {
        RequestToBeOnShift = 0,  //درخواست شیفت بودن
        RequestToBeOffShift = 1  //درخواست شیفت نبودن
    }

    public enum RequestStatus
    {
        Pending = 0,   //درحال بررسی
        Approved = 1,  //تأیید شده
        Rejected = 2  //رد شده
    }
}
