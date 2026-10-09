using AutoMapper;
using ShiftYar.Application.DTOs.DepartmentModel.DepartmentResponsibilityModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredResponsibilityModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;

namespace ShiftYar.Application.Common.Mappings
{
    public class DepartmentResponsibilityProfile : Profile
    {
        public DepartmentResponsibilityProfile()
        {
            CreateMap<DepartmentResponsibility, DepartmentResponsibilityDtoGet>()
                .ForMember(dest => dest.SpecialtyName, opt => opt.MapFrom(src => src.Specialty != null ? src.Specialty.SpecialtyName : null))
                .ForMember(dest => dest.AssignedStaffCount, opt => opt.MapFrom(src => src.UserResponsibilities != null ? src.UserResponsibilities.Count : 0));

            CreateMap<DepartmentResponsibilityDtoAdd, DepartmentResponsibility>();
            CreateMap<DepartmentResponsibilityDtoUpdate, DepartmentResponsibility>();

            CreateMap<UserDepartmentResponsibility, UserDepartmentResponsibilityDtoGet>()
                .ForMember(dest => dest.ResponsibilityId, opt => opt.MapFrom(src => src.DepartmentResponsibilityId ?? 0))
                .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.DepartmentResponsibility != null ? src.DepartmentResponsibility.Title : ""))
                .ForMember(dest => dest.IsDefault, opt => opt.MapFrom(src => src.DepartmentResponsibility != null && (src.DepartmentResponsibility.IsDefault ?? false)));

            CreateMap<ShiftRequiredResponsibility, ShiftRequiredResponsibilityDtoGet>()
                .ForMember(dest => dest.ResponsibilityTitle, opt => opt.MapFrom(src => src.DepartmentResponsibility != null ? src.DepartmentResponsibility.Title : ""))
                .ForMember(dest => dest.IsDefaultResponsibility, opt => opt.MapFrom(src => src.DepartmentResponsibility != null && (src.DepartmentResponsibility.IsDefault ?? false)));

            CreateMap<ShiftRequiredResponsibilityDtoAdd, ShiftRequiredResponsibility>();
        }
    }
}
