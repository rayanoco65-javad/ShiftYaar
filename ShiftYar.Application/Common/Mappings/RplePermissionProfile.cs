using AutoMapper;
using ShiftYar.Application.DTOs.RolePermissionModel;
using ShiftYar.Domain.Entities.RolePermissionModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class RplePermissionProfile : Profile
    {
        public RplePermissionProfile()
        {
            //CreateMap<RolePermission, RolePermissionDtoGet>();
            //CreateMap<RolePermissionDtoAdd, RolePermission>();

            // Add RolePermission mappings
            CreateMap<RolePermission, RolePermissionDtoGet>()
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.Name))
                .ForMember(dest => dest.PermissionName, opt => opt.MapFrom(src => src.Permission.Name));

            CreateMap<RolePermissionDtoAdd, RolePermission>();
        }
    }
}
