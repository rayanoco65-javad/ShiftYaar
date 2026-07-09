using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel
{
    public class ShiftRequestDtoUpdateByUser
    {
        public int Id { get; set; }
        public string? RequestPersianDate { get; set; }  //تاریخ شمسی مورد درخواست کاربر
        public RequestType? RequestType { get; set; }
        public ShiftLabel? ShiftLabel { get; set; }
        public RequestAction? RequestAction { get; set; }
        public string? Reason { get; set; }
    }
}
