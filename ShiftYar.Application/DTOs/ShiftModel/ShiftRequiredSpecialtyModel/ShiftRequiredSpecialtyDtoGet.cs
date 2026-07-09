using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredSpecialtyModel
{
    public class ShiftRequiredSpecialtyDtoGet
    {
        public int Id { get; set; }
        public int ShiftId { get; set; }
        public ShiftDtoGet Shift { get; set; }

        public int SpecialtyId { get; set; }
        public SpecialtyDtoGet Specialty { get; set; }

        public int? RequiredMaleCount { get; set; } // حداقل تعداد نیروهای مرد
        public int? RequiredFemaleCount { get; set; } // حداقل تعداد نیروهای زن
        public int? RequiredTottalCount { get; set; }  //تعداد کل نیروهای موردنیاز در شیفت صرفنظر از جنسیت

        public int? OnCallMaleCount { get; set; } // حداقل تعداد نیروهای مرد آنکال
        public int? OnCallFemaleCount { get; set; } // حداقل تعداد نیروهای زن آنکال
        public int? OnCallTottalCount { get; set; }  //تعداد کل نیروهای آنکال شیفت صرفنظر از جنسیت

    }
}
