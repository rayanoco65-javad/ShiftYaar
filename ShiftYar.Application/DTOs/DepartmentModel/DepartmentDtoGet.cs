using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.DepartmentModel
{
    public class DepartmentDtoGet
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public int? HospitalId { get; set; }
        public HospitalDto? Hospital { get; set; }
        public int? SupervisorId { get; set; }
        public SupervisorDto? Supervisor { get; set; }
        public bool? IsNightLover { get; set; }  //این بخش شب دوست است یا شب گریز. برای تقسیم شیفتهای شب
        public List<UserDto>? DepartmentUsers { get; set; }
    }

    public class HospitalDto
    {
        public int Id { get; set; }
        public string? SiamCode { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumbers { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public string? Logo { get; set; }
    }

    public class SupervisorDto
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumberMembership { get; set; }
        public string? NationalCode { get; set; }
        public string? PersonnelCode { get; set; }
        public DateTime? DateOfEmployment { get; set; }
        public bool? IsProjectPersonnel { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public bool? IsActive { get; set; }
        public string? Image { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumberMembership { get; set; }
        public string? NationalCode { get; set; }
        public string? PersonnelCode { get; set; }
        public DateTime? DateOfEmployment { get; set; }
        public bool? IsProjectPersonnel { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public bool? IsActive { get; set; }
        public string? Image { get; set; }
    }
}