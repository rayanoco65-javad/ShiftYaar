using AutoMapper;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class ShiftProfile : Profile
    {
        public ShiftProfile()
        {
            CreateMap<Shift, ShiftDtoGet>()
                .ForMember(dest => dest.Department, opt => opt.MapFrom(src => src.Department))
                .ForMember(dest => dest.RequiredSpecialties, opt => opt.MapFrom(src => src.RequiredSpecialties));

            CreateMap<Department, DepartmentInfoDto>();
            CreateMap<Hospital, HospitalInfoDto>();
            CreateMap<User, SupervisorInfoDto>();
            CreateMap<ShiftRequiredSpecialty, ShiftRequiredSpecialtyDto>()
                .ForMember(dest => dest.SpecialtyName, opt => opt.MapFrom(src => src.Specialty.SpecialtyName));

            CreateMap<ShiftDtoAdd, Shift>();
        }
    }
}
