using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredSpecialtyModel
{
    public class ShiftRequiredSpecialtyDtoGet
    {
        public int Id { get; set; }
        public int ShiftId { get; set; }
        public ShiftDtoGet Shift { get; set; }

        public int SpecialtyId { get; set; }
        public SpecialtyDtoGet Specialty { get; set; }

        public int? RequiredMaleCount { get; set; }
        public int? RequiredFemaleCount { get; set; }
        public int? RequiredTottalCount { get; set; }

        public int? OnCallMaleCount { get; set; }
        public int? OnCallFemaleCount { get; set; }
        public int? OnCallTottalCount { get; set; }

        public int? HolidayRequiredMaleCount { get; set; }
        public int? HolidayRequiredFemaleCount { get; set; }
        public int? HolidayRequiredTottalCount { get; set; }

        public int? HolidayOnCallMaleCount { get; set; }
        public int? HolidayOnCallFemaleCount { get; set; }
        public int? HolidayOnCallTottalCount { get; set; }
    }
}
