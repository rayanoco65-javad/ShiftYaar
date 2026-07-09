using AutoMapper;
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
            CreateMap<User, SupervisorDto>();
            CreateMap<User, UserDto>();
            CreateMap<DepartmentDtoAdd, Department>();

            // DepartmentName mappings
            CreateMap<DepartmentName, DepartmentNameDtoGet>();
            CreateMap<DepartmentNameDtoAdd, DepartmentName>();
        }
    }
}
