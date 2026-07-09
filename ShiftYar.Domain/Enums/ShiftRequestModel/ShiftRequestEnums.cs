using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Enums.ShiftRequestModel
{
    public enum RequestType
    {
        FullDay = 0,        //تمام روز
        SpecificShift = 1   //یک شیفت مشخص
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
