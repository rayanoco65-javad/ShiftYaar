using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel
{
    public class ShiftRequestForLeaveDtoAdd
    {
        public int? UserId { get; set; }  //کاربر درخواست دهنده
        public string? StartPersianDate { get; set; } //تاریخ شمسی شروع مرخصی مورد درخواست
        public string? EndPersianDate { get; set; } //تاریخ شمسی پایان مرخصی مورد درخواست
        public string? Reason { get; set; }

        //public RequestType? RequestType { get; set; }    //برای مرخصی بایستی مقدار صفر به معنای تمام روز در این فیلد قرار گیرد
        //public ShiftLabel? ShiftLabel { get; set; }
        //public RequestAction? RequestAction { get; set; }  //برای مرخصی بایستی مقدار یک1 به معنای شیفت نباشم در ایم فیلد قرار گیرد.

    }
}
