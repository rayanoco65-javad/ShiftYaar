using AutoMapper;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Domain.Entities.RoleModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class RoleProfile : Profile
    {
        public RoleProfile()
        {
            CreateMap<Role, RoleDtoGet>();
            CreateMap<RoleDtoAdd, Role>();
        }
    }
}
