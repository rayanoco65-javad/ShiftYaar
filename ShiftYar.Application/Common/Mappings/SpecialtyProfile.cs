using AutoMapper;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;

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
            CreateMap<User, SupervisorDto>()
                .ForMember(dest => dest.DateOfEmployment, opt => opt.MapFrom(src =>
                    DateConverter.NormalizeEmploymentDate(src.DateOfEmployment)));
            CreateMap<SpecialtyDtoAdd, Specialty>();

            // SpecialtyName mappings
            CreateMap<SpecialtyName, SpecialtyNameDtoGet>();
            CreateMap<SpecialtyNameDtoAdd, SpecialtyName>();
        }
    }
}
