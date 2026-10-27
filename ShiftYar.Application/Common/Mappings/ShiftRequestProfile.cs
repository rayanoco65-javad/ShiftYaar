using AutoMapper;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using System;
using System.Globalization;

namespace ShiftYar.Application.Common.Mappings
{
    public class ShiftRequestProfile : Profile
    {
        public ShiftRequestProfile()
        {
            var pc = new PersianCalendar();

            CreateMap<ShiftRequest, ShiftRequestDtoGet>()
                .ForMember(dest => dest.SupervisorFullName, opt => opt.MapFrom(src => src.Supervisor != null ? src.Supervisor.FullName : null))
                .ForMember(dest => dest.UserFullName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : null))
                .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.User != null ? src.User.DepartmentId : null))
                .ForMember(dest => dest.RequestPersianDate, opt => opt.MapFrom(src => src.RequestDate.HasValue ? DateConverter.ConvertToPersianDate(src.RequestDate.Value) : null))
                .ForMember(dest => dest.PersianYear, opt => opt.MapFrom(src => src.RequestDate.HasValue ? pc.GetYear(src.RequestDate.Value.Date) : (int?)null))
                .ForMember(dest => dest.PersianMonth, opt => opt.MapFrom(src => src.RequestDate.HasValue ? pc.GetMonth(src.RequestDate.Value.Date) : (int?)null))
                .ForMember(dest => dest.PersianDay, opt => opt.MapFrom(src => src.RequestDate.HasValue ? pc.GetDayOfMonth(src.RequestDate.Value.Date) : (int?)null));

            CreateMap<ShiftRequestDtoAdd, ShiftRequest>();
        }
    }
}
