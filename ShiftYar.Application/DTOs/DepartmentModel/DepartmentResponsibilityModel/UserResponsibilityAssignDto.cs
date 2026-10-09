using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.DepartmentModel.DepartmentResponsibilityModel
{
    public class UserResponsibilityAssignDto
    {
        [Required(ErrorMessage = "شناسه کاربر الزامی است")]
        public int UserId { get; set; }

        public List<int> ResponsibilityIds { get; set; } = new List<int>();
    }

    public class UserDepartmentResponsibilityDtoGet
    {
        public int ResponsibilityId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class DepartmentStaffMatrixDto
    {
        public int DepartmentId { get; set; }
        public List<DepartmentResponsibilityDtoGet> Responsibilities { get; set; } = new List<DepartmentResponsibilityDtoGet>();
        public List<StaffResponsibilityMatrixItemDto> Staff { get; set; } = new List<StaffResponsibilityMatrixItemDto>();
    }

    public class StaffResponsibilityMatrixItemDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? PersonnelCode { get; set; }
        public int? SpecialtyId { get; set; }
        public string? SpecialtyName { get; set; }
        public List<int> AssignedResponsibilityIds { get; set; } = new List<int>();
    }

    public class BatchAssignResponsibilitiesDto
    {
        [Required(ErrorMessage = "شناسه بخش الزامی است")]
        public int DepartmentId { get; set; }

        public List<UserResponsibilityAssignDto> Assignments { get; set; } = new List<UserResponsibilityAssignDto>();
    }
}
