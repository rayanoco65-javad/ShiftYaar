using AutoMapper;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.RoleModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using System.Linq;

namespace ShiftYar.Application.Common.Mappings
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserDtoAdd>()
                .ForMember(dest => dest.OtherPhoneNumbers, opt => opt.MapFrom(src =>
                    src.OtherPhoneNumbers != null ? src.OtherPhoneNumbers.Select(p => p.PhoneNumber).ToList() : null))
                .ForMember(dest => dest.UserRoles, opt => opt.MapFrom(src =>
                    src.UserRoles != null ? src.UserRoles.Select(r => r.RoleId).ToList() : null))
                .ForMember(dest => dest.DateOfEmployment, opt => opt.MapFrom(src =>
                    DateConverter.EmploymentDateToPersianString(src.DateOfEmployment)));

            CreateMap<UserDtoAdd, User>()
                .ForMember(dest => dest.OtherPhoneNumbers, opt => opt.MapFrom(src =>
                    src.OtherPhoneNumbers != null ? src.OtherPhoneNumbers.Select(p => new UserPhoneNumber { PhoneNumber = p }).ToList() : null))
                .ForMember(dest => dest.UserRoles, opt => opt.MapFrom(src =>
                    src.UserRoles != null ? src.UserRoles.Select(r => new UserRole { RoleId = r }).ToList() : null))
                // تاریخ استخدام شمسی است؛ تبدیل در UserService انجام می‌شود تا AutoMapper اشتباه پارس نکند.
                .ForMember(dest => dest.DateOfEmployment, opt => opt.Ignore());

            CreateMap<UserPhoneNumber, UserPhoneNumber>()
                .ForMember(dest => dest.User, opt => opt.Ignore());

            CreateMap<UserRole, UserRole>()
                .ForMember(dest => dest.User, opt => opt.Ignore());

            CreateMap<User, UserDtoGet>()
                .ForMember(dest => dest.DateOfEmployment, opt => opt.MapFrom(src =>
                    DateConverter.NormalizeEmploymentDate(src.DateOfEmployment)));

            CreateMap<Department, UserDepartmentDtoGet>();
            CreateMap<Specialty, UserSpecialtyDtoGet>();
            CreateMap<UserPhoneNumber, UserPhoneNumberDtoGet>();
            CreateMap<UserRole, UserRoleDtoGet>();
            CreateMap<Role, RoleDtoGet>();

            CreateMap<UserDtoGet, User>()
                .ForMember(dest => dest.Department, opt => opt.Ignore())
                .ForMember(dest => dest.Specialty, opt => opt.Ignore())
                .ForMember(dest => dest.OtherPhoneNumbers, opt => opt.MapFrom(src =>
                    src.OtherPhoneNumbers != null
                        ? src.OtherPhoneNumbers.Select(p => new UserPhoneNumber
                        {
                            Id = p.Id,
                            PhoneNumber = p.PhoneNumber,
                            UserId = p.UserId,
                            IsActive = p.IsActive
                        }).ToList()
                        : null))
                .ForMember(dest => dest.UserRoles, opt => opt.MapFrom(src =>
                    src.UserRoles != null
                        ? src.UserRoles.Select(r => new UserRole
                        {
                            Id = r.Id,
                            UserId = r.UserId,
                            RoleId = r.RoleId
                        }).ToList()
                        : null));

            // DepartmentSchedulingSettings mappings
            CreateMap<DepartmentSchedulingSettings, DepartmentSchedulingSettingsDtoGet>();
            CreateMap<DepartmentSchedulingSettingsDtoAdd, DepartmentSchedulingSettings>();
        }
    }
}
