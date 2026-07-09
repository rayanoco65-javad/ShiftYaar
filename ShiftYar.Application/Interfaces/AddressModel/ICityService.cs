using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Features.AddressModel.Filters;
using ShiftYar.Domain.Entities.AddressModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.AddressModel
{
    public interface ICityService
    {
        Task<ApiResponse<PagedResponse<City>>> GetCities(CityFilter filter);
    }
}
