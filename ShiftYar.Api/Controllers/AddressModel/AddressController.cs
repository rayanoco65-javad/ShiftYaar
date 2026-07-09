using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.AddressModel.Filters;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using ShiftYar.Application.Interfaces.AddressModel;
using ShiftYar.Domain.Entities.AddressModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.AddressModel
{
    public class AddressController : BaseController
    {
        private readonly IProvinceService _provinceService;
        private readonly ICityService _cityService;

        public AddressController(ShiftYarDbContext context, ICityService cityService, IProvinceService provinceService) : base(context)
        {
            _provinceService = provinceService;
            _cityService = cityService;
        }

        ///Get All Provinces
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<Province>>>> GetProvinces([FromQuery] ProvinceFilter filter)
        {
            try
            {
                var result = await _provinceService.GetProvinces(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, Message = "عملیات دریافت لیست استان ها با خطا مواجه شد : " + ex.Message });
            }
        }


        ///Get All Cities
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<City>>>> GetCities([FromQuery] CityFilter filter)
        {
            try
            {
                var result = await _cityService.GetCities(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, Message = "عملیات دریافت لیست شهرها با خطا مواجه شد : " + ex.Message });
            }
        }

    }
}
