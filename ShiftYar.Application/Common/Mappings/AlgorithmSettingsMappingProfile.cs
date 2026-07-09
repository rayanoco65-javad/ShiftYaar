using AutoMapper;
using ShiftYar.Application.DTOs.Settings;
using ShiftYar.Domain.Entities.Settings;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Mappings
{
    public class AlgorithmSettingsMappingProfile : Profile
    {
        public AlgorithmSettingsMappingProfile()
        {
            CreateMap<AlgorithmSettingsDtoAdd, AlgorithmSettings>()
                .ForMember(dest => dest.SA_InitialTemperature, opt => opt.MapFrom(src => src.SA_InitialTemperature))
                .ForMember(dest => dest.SA_FinalTemperature, opt => opt.MapFrom(src => src.SA_FinalTemperature))
                .ForMember(dest => dest.SA_CoolingRate, opt => opt.MapFrom(src => src.SA_CoolingRate))
                .ForMember(dest => dest.SA_MaxIterations, opt => opt.MapFrom(src => src.SA_MaxIterations))
                .ForMember(dest => dest.SA_MaxIterationsWithoutImprovement, opt => opt.MapFrom(src => src.SA_MaxIterationsWithoutImprovement))
                .ForMember(dest => dest.ORT_MaxTimeInSeconds, opt => opt.MapFrom(src => src.ORT_MaxTimeInSeconds))
                .ForMember(dest => dest.ORT_NumSearchWorkers, opt => opt.MapFrom(src => src.ORT_NumSearchWorkers))
                .ForMember(dest => dest.ORT_LogSearchProgress, opt => opt.MapFrom(src => src.ORT_LogSearchProgress))
                .ForMember(dest => dest.ORT_MaxSolutions, opt => opt.MapFrom(src => src.ORT_MaxSolutions))
                .ForMember(dest => dest.ORT_RelativeGapLimit, opt => opt.MapFrom(src => src.ORT_RelativeGapLimit))
                .ForMember(dest => dest.HYB_Strategy, opt => opt.MapFrom(src => src.HYB_Strategy))
                .ForMember(dest => dest.HYB_MaxIterations, opt => opt.MapFrom(src => src.HYB_MaxIterations))
                .ForMember(dest => dest.HYB_ComplexityThreshold, opt => opt.MapFrom(src => src.HYB_ComplexityThreshold))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Department, opt => opt.Ignore())
                .ForMember(dest => dest.CreateDate, opt => opt.Ignore())
                .ForMember(dest => dest.UpdateDate, opt => opt.Ignore())
                .ForMember(dest => dest.TheUserId, opt => opt.Ignore());

            CreateMap<AlgorithmSettings, AlgorithmSettingsDtoGet>()
                .ForMember(dest => dest.SA_InitialTemperature, opt => opt.MapFrom(src => src.SA_InitialTemperature))
                .ForMember(dest => dest.SA_FinalTemperature, opt => opt.MapFrom(src => src.SA_FinalTemperature))
                .ForMember(dest => dest.SA_CoolingRate, opt => opt.MapFrom(src => src.SA_CoolingRate))
                .ForMember(dest => dest.SA_MaxIterations, opt => opt.MapFrom(src => src.SA_MaxIterations))
                .ForMember(dest => dest.SA_MaxIterationsWithoutImprovement, opt => opt.MapFrom(src => src.SA_MaxIterationsWithoutImprovement))
                .ForMember(dest => dest.ORT_MaxTimeInSeconds, opt => opt.MapFrom(src => src.ORT_MaxTimeInSeconds))
                .ForMember(dest => dest.ORT_NumSearchWorkers, opt => opt.MapFrom(src => src.ORT_NumSearchWorkers))
                .ForMember(dest => dest.ORT_LogSearchProgress, opt => opt.MapFrom(src => src.ORT_LogSearchProgress))
                .ForMember(dest => dest.ORT_MaxSolutions, opt => opt.MapFrom(src => src.ORT_MaxSolutions))
                .ForMember(dest => dest.ORT_RelativeGapLimit, opt => opt.MapFrom(src => src.ORT_RelativeGapLimit))
                .ForMember(dest => dest.HYB_Strategy, opt => opt.MapFrom(src => src.HYB_Strategy))
                .ForMember(dest => dest.HYB_MaxIterations, opt => opt.MapFrom(src => src.HYB_MaxIterations))
                .ForMember(dest => dest.HYB_ComplexityThreshold, opt => opt.MapFrom(src => src.HYB_ComplexityThreshold))
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : null))
                .ForMember(dest => dest.AlgorithmTypeName, opt => opt.Ignore()); // در سرویس تنظیم می‌شود
        }
    }
}
