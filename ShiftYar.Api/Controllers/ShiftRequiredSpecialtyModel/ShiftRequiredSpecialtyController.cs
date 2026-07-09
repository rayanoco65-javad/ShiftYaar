using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredSpecialtyModel;
using ShiftYar.Application.Features.ShiftRequiredSpecialtyModel.Filters;
using ShiftYar.Application.Interfaces.ShiftRequiredSpecialtyModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.ShiftRequiredSpecialtyModel
{
    public class ShiftRequiredSpecialtyController : BaseController
    {
        private readonly IShiftRequiredSpecialtyService _shiftRequiredSpecialtyService;
        private readonly ILogger<ShiftRequiredSpecialtyController> _logger;
        public ShiftRequiredSpecialtyController(ShiftYarDbContext context, IShiftRequiredSpecialtyService shiftRequiredSpecialtyService , ILogger<ShiftRequiredSpecialtyController> logger) : base(context)
        {
            _shiftRequiredSpecialtyService = shiftRequiredSpecialtyService;
            _logger = logger;
        }

        ///Get All Shift Required Specialties
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<ShiftRequiredSpecialtyDtoGet>>>> GetShiftRequiredSpecialties([FromQuery] ShiftRequiredSpecialtyFilter filter)
        {
            try
            {
                var result = await _shiftRequiredSpecialtyService.GetShiftRequiredSpecialties(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات دریافت نیازمندیهای شیفت با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Get Shift Required Specialty By Id
        [HttpGet]
        public async Task<ActionResult<ApiResponse<ShiftRequiredSpecialtyDtoGet>>> GetShiftRequiredSpecialty(int id)
        {
            try
            {
                var result = await _shiftRequiredSpecialtyService.GetShiftRequiredSpecialty(id);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات دریافت نیازمندی شیفت با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Create Shift Required Specialty
        [HttpPost]
        public async Task<ActionResult<ApiResponse<ShiftRequiredSpecialtyDtoGet>>> CreateShiftRequiredSpecialty([FromBody] ShiftRequiredSpecialtyDtoAdd dto)
        {
            try
            {
                var result = await _shiftRequiredSpecialtyService.CreateShiftRequiredSpecialty(dto);
                if (!result.IsSuccess)
                {
                    return BadRequest(result);
                }
                return CreatedAtAction(nameof(GetShiftRequiredSpecialty), new { id = result.Data.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ایجاد نیازمندی شیفت با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Update Shift Required Specialty
        [HttpPut]
        public async Task<ActionResult<ApiResponse<ShiftRequiredSpecialtyDtoGet>>> UpdateShiftRequiredSpecialty(int id, [FromBody] ShiftRequiredSpecialtyDtoAdd dto)
        {
            try
            {
                var result = await _shiftRequiredSpecialtyService.UpdateShiftRequiredSpecialty(id, dto);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ویرایش نیازمندی شیفت با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Delete Shift Required Specialty
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteShiftRequiredSpecialty(int id)
        {
            try
            {
                var result = await _shiftRequiredSpecialtyService.DeleteShiftRequiredSpecialty(id);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات حذف نیازمندی شیفت با خطا مواجه شد : " + ex.Message);
            }
        }

    }
}
