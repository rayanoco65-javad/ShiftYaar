using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Features.AddressModel.Filters;
using ShiftYar.Domain.Entities.AddressModel;

namespace ShiftYar.Application.Interfaces.AddressModel
{
    public interface IProvinceService
    {
        Task<ApiResponse<PagedResponse<Province>>> GetProvinces(ProvinceFilter filter);
    }
}
