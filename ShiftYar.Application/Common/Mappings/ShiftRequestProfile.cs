using AutoMapper;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class ShiftRequestProfile : Profile
    {
        public ShiftRequestProfile()
        {
            CreateMap<ShiftRequest, ShiftRequestDtoGet>()
                .ForMember(dest => dest.SupervisorFullName, opt => opt.MapFrom(src => src.Supervisor != null ? src.Supervisor.FullName : null));

            CreateMap<ShiftRequestDtoAdd, ShiftRequest>();
        }
    }
}
