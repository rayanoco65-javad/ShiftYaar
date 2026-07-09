using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredSpecialtyModel
{
    public class ShiftRequiredSpecialtyDtoAdd
    {
        [Required(ErrorMessage = "شناسه شیفت الزامی است")]
        public int ShiftId { get; set; }

        [Required(ErrorMessage = "شناسه تخصص الزامی است")]
        public int SpecialtyId { get; set; }

        public int? RequiredMaleCount { get; set; } // حداقل تعداد نیروهای مرد
        public int? RequiredFemaleCount { get; set; } // حداقل تعداد نیروهای زن

        [Required(ErrorMessage = "تعداد مورد نیاز الزامی است")]
        [Range(1, int.MaxValue, ErrorMessage = "تعداد مورد نیاز باید بزرگتر از صفر باشد")]
        public int? RequiredTottalCount { get; set; }  //تعداد کل نیروهای موردنیاز در شیفت صرفنظر از جنسیت

        public int? OnCallMaleCount { get; set; } // حداقل تعداد نیروهای مرد آنکال
        public int? OnCallFemaleCount { get; set; } // حداقل تعداد نیروهای زن آنکال
        public int? OnCallTottalCount { get; set; }  //تعداد کل نیروهای آنکال شیفت صرفنظر از جنسیت
    }
}
