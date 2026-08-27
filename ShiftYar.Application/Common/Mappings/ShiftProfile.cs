using AutoMapper;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;

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

            // Manager counts are applied explicitly in ShiftService after NormalizeShiftDto
            // so partial updates don't wipe values via AutoMapper.
            CreateMap<ShiftDtoAdd, Shift>()
                .ForMember(dest => dest.ManagerRequiredCount, opt => opt.Ignore())
                .ForMember(dest => dest.ManagerMinLevel1Count, opt => opt.Ignore());
        }
    }
}
