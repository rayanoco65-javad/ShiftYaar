using ShiftYar.Domain.Enums.ShiftExchangeModel;
using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.ShiftExchangeModel
{
    public class ShiftExchangeDtoAdd
    {
        [Required(ErrorMessage = "شناسه کاربر درخواست کننده الزامی است")]
        public int RequestingUserId { get; set; }

        [Required(ErrorMessage = "شناسه کاربر پیشنهاد دهنده الزامی است")]
        public int OfferingUserId { get; set; }

        [Required(ErrorMessage = "شناسه شیفت درخواست کننده الزامی است")]
        public int RequestingShiftAssignmentId { get; set; }

        /// <summary>
        /// فقط برای <see cref="ExchangeType.Swap"/> الزامی است.
        /// برای <see cref="ExchangeType.Transfer"/> باید خالی باشد.
        /// </summary>
        public int? OfferingShiftAssignmentId { get; set; }

        /// <summary>
        /// 0 = Swap (جابجایی دوطرفه)، 1 = Transfer (واگذاری به کاربر آزاد).
        /// </summary>
        public ExchangeType ExchangeType { get; set; } = ExchangeType.Swap;

        [Required(ErrorMessage = "دلیل درخواست الزامی است")]
        [StringLength(500, ErrorMessage = "دلیل درخواست نمی‌تواند بیش از 500 کاراکتر باشد")]
        public string Reason { get; set; } = string.Empty;
    }
}
