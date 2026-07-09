using AutoMapper;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredSpecialtyModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class ShiftRequiredSpecialtyProfile : Profile
    {
        public ShiftRequiredSpecialtyProfile()
        {
            //CreateMap<ShiftRequiredSpecialty, ShiftRequiredSpecialtyDtoGet>();
            CreateMap<ShiftRequiredSpecialty, ShiftRequiredSpecialtyDtoGet>()
                .ForMember(dest => dest.Shift, opt => opt.MapFrom(src => src.Shift));
            CreateMap<ShiftRequiredSpecialtyDtoAdd, ShiftRequiredSpecialty>();
            CreateMap<Shift, ShiftDtoGet>();
            CreateMap<Specialty, SpecialtyDtoGet>();
        }
    }
}
