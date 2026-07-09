using AutoMapper;
using ShiftYar.Application.DTOs.HospitalModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Mappings
{
    public class HospitalProfile : Profile
    {
        public HospitalProfile()
        {
            CreateMap<Hospital, HospitalDtoAdd>()
                .ForMember(dest => dest.PhoneNumbers, opt => opt.MapFrom(src =>
                    src.PhoneNumbers != null ? src.PhoneNumbers.Select(p => p.PhoneNumber).ToList() : null));

            CreateMap<HospitalDtoAdd, Hospital>()
                .ForMember(dest => dest.PhoneNumbers, opt => opt.MapFrom(src =>
                    src.PhoneNumbers != null ? src.PhoneNumbers.Select(p => new HospitalPhoneNumber { PhoneNumber = p }).ToList() : null));

            ////فقط شماره ها رو نمایش میده
            //CreateMap<Hospital, HospitalDtoGet>()
            //    .ForMember(dest => dest.PhoneNumbers, opt => opt.MapFrom(src =>
            //        src.PhoneNumbers != null ? src.PhoneNumbers.Select(p => p.PhoneNumber).ToList() : null));

            CreateMap<HospitalPhoneNumber, HospitalPhoneNumber>()
                .ForMember(dest => dest.Hospital, opt => opt.Ignore());

            CreateMap<Hospital, HospitalDtoGet>()
                .ForMember(dest => dest.PhoneNumbers, opt => opt.MapFrom(src =>
                    src.PhoneNumbers != null ? src.PhoneNumbers.Select(p => new HospitalPhoneNumber
                    {
                        Id = p.Id,
                        HospitalId = p.HospitalId,
                        PhoneNumber = p.PhoneNumber,
                        IsActive = p.IsActive
                    }).ToList() : null));
        }
    }
}
