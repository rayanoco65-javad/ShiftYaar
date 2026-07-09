using AutoMapper;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.AddressModel.Filters;
using ShiftYar.Application.Features.DepartmentModel.Services;
using ShiftYar.Application.Interfaces.AddressModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.AddressModel;
using ShiftYar.Domain.Entities.DepartmentModel;

namespace ShiftYar.Application.Features.AddressModel.Services
{
    public class ProvinceService : IProvinceService
    {
        private readonly IEfRepository<Province> _repository;
        private readonly ILogger<ProvinceService> _logger;
        public ProvinceService(IEfRepository<Province> repository, ILogger<ProvinceService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<ApiResponse<PagedResponse<Province>>> GetProvinces(ProvinceFilter filter)
        {
            try
            {
                _logger.LogInformation("Fetching Provinces with filter: {@Filter}", filter);
                var result = await _repository.GetByFilterAsync(filter);

                var pagedResponse = new PagedResponse<Province>
                {
                    Items = result.Items,
                    TotalCount = result.TotalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
                };

                _logger.LogInformation("Successfully fetched {Count} Provinces out of {TotalCount}", result.Items.Count, result.TotalCount);
                return ApiResponse<PagedResponse<Province>>.Success(pagedResponse);
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت لیست استان ها با خطا مواجه شد." + ex.Message);
            }
        }
    }
}
