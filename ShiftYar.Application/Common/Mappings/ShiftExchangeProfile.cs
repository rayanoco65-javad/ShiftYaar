using AutoMapper;
using ShiftYar.Application.DTOs.ShiftExchangeModel;
using ShiftYar.Domain.Entities.ShiftExchangeModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class ShiftExchangeProfile : Profile
    {
        public ShiftExchangeProfile()
        {
            CreateMap<ShiftExchange, ShiftExchangeDtoGet>()
                .ForMember(dest => dest.RequestingUserFullName, opt => opt.MapFrom(src => src.RequestingUser != null ? src.RequestingUser.FullName : null))
                .ForMember(dest => dest.OfferingUserFullName, opt => opt.MapFrom(src => src.OfferingUser != null ? src.OfferingUser.FullName : null))
                .ForMember(dest => dest.SupervisorFullName, opt => opt.MapFrom(src => src.Supervisor != null ? src.Supervisor.FullName : null))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreateDate))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdateDate));

            CreateMap<ShiftExchangeDtoAdd, ShiftExchange>();
            CreateMap<ShiftExchangeDtoUpdate, ShiftExchange>();
        }
    }
}
