using AutoMapper;
using ShiftYar.Application.DTOs.PermissionModel;
using ShiftYar.Domain.Entities.PermissionModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class PermissionProfile : Profile
    {
        public PermissionProfile()
        {
            CreateMap<Permission, PermissionDtoGet>();
            CreateMap<PermissionDtoAdd, Permission>();
        }
    }
}
