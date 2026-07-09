using AutoMapper;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
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
    public class SpecialtyProfile : Profile
    {
        public SpecialtyProfile()
        {
            CreateMap<Specialty, SpecialtyDtoGet>()
                .ForMember(dest => dest.Department, opt => opt.MapFrom(src => src.Department));
            CreateMap<Hospital, HospitalDto>();
            CreateMap<Department, DepartmentDto>();
            CreateMap<User, SupervisorDto>();
            CreateMap<SpecialtyDtoAdd, Specialty>();

            // SpecialtyName mappings
            CreateMap<SpecialtyName, SpecialtyNameDtoGet>();
            CreateMap<SpecialtyNameDtoAdd, SpecialtyName>();
        }
    }
}
