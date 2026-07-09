using AutoMapper;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class ShiftScheduleProfile : Profile
    {
        public ShiftScheduleProfile()
        {
            CreateMap<ShiftAssignment, ShiftScheduleDtoGet>()
                // Id
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id ?? 0))

                // ShiftDate
                .ForMember(dest => dest.ShiftDateId, opt => opt.MapFrom(src => src.ShiftDateId))
                .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.ShiftDate != null ? src.ShiftDate.Date : (DateTime?)null))
                .ForMember(dest => dest.PersianDate, opt => opt.MapFrom(src => src.ShiftDate != null ? src.ShiftDate.PersianDate : null))
                .ForMember(dest => dest.DayTitle, opt => opt.MapFrom(src => src.ShiftDate != null ? src.ShiftDate.DayTitle : null))
                .ForMember(dest => dest.IsHoliday, opt => opt.MapFrom(src => src.ShiftDate != null ? src.ShiftDate.IsHoliday : null))
                .ForMember(dest => dest.HolidayEvent, opt => opt.MapFrom(src => src.ShiftDate != null ? src.ShiftDate.HolidayEvent : null))

                // Shift
                .ForMember(dest => dest.ShiftId, opt => opt.MapFrom(src => src.ShiftId))
                .ForMember(dest => dest.ShiftTitle, opt => opt.MapFrom(src => src.Shift != null ? src.Shift.Label : null))
                .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Shift != null ? src.Shift.StartTime : null))
                .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.Shift != null ? src.Shift.EndTime : null))

                // User
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
                .ForMember(dest => dest.UserFullName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : null))
                .ForMember(dest => dest.PersonnelCode, opt => opt.MapFrom(src => src.User != null ? src.User.PersonnelCode : null))
                .ForMember(dest => dest.NationalCode, opt => opt.MapFrom(src => src.User != null ? src.User.NationalCode : null))
                .ForMember(dest => dest.UserPhoneNumber, opt => opt.MapFrom(src => src.User != null ? src.User.PhoneNumberMembership : null))
                .ForMember(dest => dest.IsProjectPersonnel, opt => opt.MapFrom(src => src.User != null ? src.User.IsProjectPersonnel : null))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.User != null ? src.User.IsActive : null))
                .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.User != null ? src.User.Gender : null))

                // Department
                .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.User != null ? src.User.DepartmentId : null))
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.User != null && src.User.Department != null ? src.User.Department.Name : null))
                .ForMember(dest => dest.DepartmentIsNightLover, opt => opt.MapFrom(src => src.User != null && src.User.Department != null ? src.User.Department.IsNightLover : null))

                // Specialty
                .ForMember(dest => dest.SpecialtyId, opt => opt.MapFrom(src => src.User != null ? src.User.SpecialtyId : null))
                .ForMember(dest => dest.SpecialtyName, opt => opt.MapFrom(src => src.User != null && src.User.Specialty != null ? src.User.Specialty.SpecialtyName : null))

                // Assignment
                .ForMember(dest => dest.IsOnCall, opt => opt.MapFrom(src => src.IsOnCall))
                .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Notes));
        }
    }

}
