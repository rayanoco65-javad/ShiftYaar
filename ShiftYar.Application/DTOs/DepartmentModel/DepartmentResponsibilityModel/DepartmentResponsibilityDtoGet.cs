namespace ShiftYar.Application.DTOs.DepartmentModel.DepartmentResponsibilityModel
{
    public class DepartmentResponsibilityDtoGet
    {
        public int Id { get; set; }
        public int? DepartmentId { get; set; }
        public int? SpecialtyId { get; set; }
        public string? SpecialtyName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool? IsDefault { get; set; }
        public int? Priority { get; set; }
        public bool? IsActive { get; set; }
        public int AssignedStaffCount { get; set; }
    }
}
