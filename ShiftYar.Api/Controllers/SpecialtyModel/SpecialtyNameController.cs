using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using ShiftYar.Application.Features.SpecialtyModel.Filters;
using ShiftYar.Application.Interfaces.SpecialtyModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.SpecialtyModel
{
    public class SpecialtyNameController : BaseController
    {
        private readonly ISpecialtyNameService _specialtyNameService;

        public SpecialtyNameController(ShiftYarDbContext context, ISpecialtyNameService specialtyNameService) : base(context)
        {
            _specialtyNameService = specialtyNameService;
        }

        ///Get All Specialty Names
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<SpecialtyNameDtoGet>>>> GetSpecialtyNames([FromQuery] SpecialtyNameFilter filter)
        {
            var result = await _specialtyNameService.GetSpecialtyNamesAsync(filter);
            return Ok(result);
        }

        ///Get Specialty Name
        [HttpGet]
        public async Task<ActionResult<ApiResponse<SpecialtyNameDtoGet>>> GetSpecialtyName(int id)
        {
            var result = await _specialtyNameService.GetSpecialtyNameAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        ///Create Specialty Name
        [HttpPost]
        public async Task<ActionResult<ApiResponse<SpecialtyNameDtoGet>>> CreateSpecialtyName([FromBody] SpecialtyNameDtoAdd dto)
        {
            var result = await _specialtyNameService.CreateSpecialtyNameAsync(dto);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }
            return CreatedAtAction(nameof(GetSpecialtyName), new { id = result.Data.Id }, result);
        }

        ///Update Specialty Name
        [HttpPut]
        public async Task<ActionResult<ApiResponse<SpecialtyNameDtoGet>>> UpdateSpecialtyName(int id, [FromBody] SpecialtyNameDtoAdd dto)
        {
            var result = await _specialtyNameService.UpdateSpecialtyNameAsync(id, dto);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        ///Delete Specialty Name
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteSpecialtyName(int id)
        {
            var result = await _specialtyNameService.DeleteSpecialtyNameAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }
    }
}
