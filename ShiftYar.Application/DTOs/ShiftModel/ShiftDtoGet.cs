using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel
{
    public class ShiftDtoGet
    {
        public int Id { get; set; }
        public int? DepartmentId { get; set; }
        public DepartmentInfoDto? Department { get; set; }
        public ShiftLabel? Label { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public List<ShiftRequiredSpecialtyDto>? RequiredSpecialties { get; set; }
    }

    public class DepartmentInfoDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public HospitalInfoDto? Hospital { get; set; }
        public SupervisorInfoDto? Supervisor { get; set; }
    }

    public class HospitalInfoDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? SiamCode { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumbers { get; set; }
    }

    public class SupervisorInfoDto
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumberMembership { get; set; }
        public string? NationalCode { get; set; }
        public string? PersonnelCode { get; set; }
    }

    public class ShiftRequiredSpecialtyDto
    {
        public int Id { get; set; }
        public int? SpecialtyId { get; set; }
        public string? SpecialtyName { get; set; }
        public int? RequiredMaleCount { get; set; }
        public int? RequiredFemaleCount { get; set; }
        public int? RequiredTottalCount { get; set; }
        public int? OnCallMaleCount { get; set; }
        public int? OnCallFemaleCount { get; set; }
        public int? OnCallTottalCount { get; set; }
    }

    //public class ShiftDtoGet
    //{
    //    public int? Id { get; set; }

    //    public int? DepartmentId { get; set; }
    //    public Department? Department { get; set; }

    //    public ShiftLabel? Label { get; set; } // صبح، عصر، شب

    //    public TimeSpan? StartTime { get; set; }
    //    public TimeSpan? EndTime { get; set; }

    //    //public ICollection<ShiftRequiredSpecialty>? RequiredSpecialties { get; set; }
    //}
}
