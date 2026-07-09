using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Application.Features.ShiftModel.Filters;
using ShiftYar.Application.Interfaces.ShiftModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.ShiftModel
{
    public class ShiftController : BaseController
    {
        private readonly IShiftService _shiftService;
        public ShiftController(ShiftYarDbContext context, IShiftService shiftService) : base(context)
        {
            _shiftService = shiftService;
        }

        ///Get All Shifts
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<ShiftDtoGet>>>> GetShifts([FromQuery] ShiftFilter filter)
        {
            try
            {
                var result = await _shiftService.GetShifts(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات دریافت شیفت‌ها با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Get Shift By Id
        [HttpGet]
        public async Task<ActionResult<ApiResponse<ShiftDtoGet>>> GetShift(int id)
        {
            try
            {
                var result = await _shiftService.GetShift(id);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات دریافت شیفت با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Create Shift
        [HttpPost]
        public async Task<ActionResult<ApiResponse<ShiftDtoGet>>> CreateShift([FromBody] ShiftDtoAdd dto)
        {
            try
            {
                var result = await _shiftService.CreateShift(dto);
                if (!result.IsSuccess)
                {
                    return BadRequest(result);
                }
                return CreatedAtAction(nameof(GetShift), new { id = result.Data.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ایجاد شیفت با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Update Shift
        [HttpPut]
        public async Task<ActionResult<ApiResponse<ShiftDtoGet>>> UpdateShift(int id, [FromBody] ShiftDtoAdd dto)
        {
            try
            {
                var result = await _shiftService.UpdateShift(id, dto);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ویرایش شیفت با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Delete Shift
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteShift(int id)
        {
            try
            {
                var result = await _shiftService.DeleteShift(id);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات حذف شیفت با خطا مواجه شد : " + ex.Message);
            }
        }
    }
}
