using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using ShiftYar.Application.Features.RoleModel.Filters;
using ShiftYar.Application.Features.SpecialtyModel.Filters;
using ShiftYar.Application.Interfaces.SpecialtyModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.SpecialtyModel
{
    public class SpecialtyController : BaseController
    {
        private readonly ISpecialtyService _specialtyService;
        public SpecialtyController(ShiftYarDbContext context, ISpecialtyService specialtyService) : base(context)
        {
            _specialtyService = specialtyService;
        }

        ///Get All Specialties
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<SpecialtyDtoGet>>>> GetSpecialties([FromQuery] SpecialtyFilter filter)
        {
            try
            {
                var result = await _specialtyService.GetSpecialties(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("دریافت تخصص ها با خطا مواجه شد : " + ex.Message);
            }
        }

        ///Get Specialty By Id
        [HttpGet]
        public async Task<ActionResult<ApiResponse<SpecialtyDtoGet>>> GetSpecialty(int id)
        {
            try
            {
                var result = await _specialtyService.GetSpecialty(id);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات دریافت تخصص با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Create Specialty
        [HttpPost]
        public async Task<ActionResult<ApiResponse<SpecialtyDtoGet>>> CreateSpecialty([FromBody] SpecialtyDtoAdd dto)
        {
            try
            {
                var result = await _specialtyService.CreateSpecialty(dto);
                if (!result.IsSuccess)
                {
                    return BadRequest(result);
                }
                return CreatedAtAction(nameof(GetSpecialty), new { id = result.Data.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ایجاد تخصص با خطا مواجه شد : " + ex.Message);
            }
        }

        ///Update Specialty
        [HttpPut]
        public async Task<ActionResult<ApiResponse<SpecialtyDtoGet>>> UpdateSpecialty(int id, [FromBody] SpecialtyDtoAdd dto)
        {
            try
            {
                var result = await _specialtyService.UpdateSpecialty(id, dto);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ویرایش تخصص با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Delete Specialty
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteSpecialty(int id)
        {
            try
            {
                var result = await _specialtyService.DeleteSpecialty(id);
                if (!result.IsSuccess)
                {
                    return NotFound(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات حذف تخصص با خطا مواجه شد : " + ex.Message);
            }
        }
    }
}
