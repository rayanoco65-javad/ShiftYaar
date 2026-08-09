using AutoMapper;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.UserModel;

namespace ShiftYar.Application.Common.Mappings
{
    public class DepartmentProfile : Profile
    {
        public DepartmentProfile()
        {
            CreateMap<Department, DepartmentDtoGet>()
                .ForMember(dest => dest.DepartmentUsers, opt => opt.MapFrom(src => src.DepartmentUsers));
            CreateMap<Hospital, HospitalDto>();
            CreateMap<User, SupervisorDto>()
                .ForMember(dest => dest.DateOfEmployment, opt => opt.MapFrom(src =>
                    DateConverter.NormalizeEmploymentDate(src.DateOfEmployment)));
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.DateOfEmployment, opt => opt.MapFrom(src =>
                    DateConverter.NormalizeEmploymentDate(src.DateOfEmployment)));
            CreateMap<DepartmentDtoAdd, Department>();

            // DepartmentName mappings
            CreateMap<DepartmentName, DepartmentNameDtoGet>();
            CreateMap<DepartmentNameDtoAdd, DepartmentName>();
        }
    }
}
