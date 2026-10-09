namespace ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredResponsibilityModel
{
    public class ShiftRequiredResponsibilityDtoGet
    {
        public int Id { get; set; }
        public int ShiftId { get; set; }
        public int DepartmentResponsibilityId { get; set; }
        public string ResponsibilityTitle { get; set; } = string.Empty;
        public bool IsDefaultResponsibility { get; set; }

        public int? RequiredMaleCount { get; set; }
        public int? RequiredFemaleCount { get; set; }
        public int? RequiredTotalCount { get; set; }

        public int? HolidayRequiredMaleCount { get; set; }
        public int? HolidayRequiredFemaleCount { get; set; }
        public int? HolidayRequiredTotalCount { get; set; }
    }
}
