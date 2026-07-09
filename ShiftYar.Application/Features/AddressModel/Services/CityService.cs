using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Features.AddressModel.Filters;
using ShiftYar.Application.Interfaces.AddressModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.AddressModel;

namespace ShiftYar.Application.Features.AddressModel.Services
{
    public class CityService : ICityService
    {
        private readonly IEfRepository<City> _repository;
        private readonly ILogger<CityService> _logger;
        public CityService(IEfRepository<City> repository, ILogger<CityService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<ApiResponse<PagedResponse<City>>> GetCities(CityFilter filter)
        {
            try
            {
                _logger.LogInformation("Fetching Cities with filter: {@Filter}", filter);
                var result = await _repository.GetByFilterAsync(filter, "Province");

                var pagedResponse = new PagedResponse<City>
                {
                    Items = result.Items,
                    TotalCount = result.TotalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
                };

                _logger.LogInformation("Successfully fetched {Count} Cities out of {TotalCount}", result.Items.Count, result.TotalCount);
                return ApiResponse<PagedResponse<City>>.Success(pagedResponse);
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت لیست شهر ها با خطا مواجه شد." + ex.Message);
            }
        }
    }
}
