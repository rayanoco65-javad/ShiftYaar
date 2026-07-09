using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using ShiftYar.Application.Interfaces.DepartmentModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.DepartmentModel
{
    public class DepartmentNameController : BaseController
    {
        private readonly IDepartmentNameService _departmentNameService;

        public DepartmentNameController(ShiftYarDbContext context, IDepartmentNameService departmentNameService) : base(context)
        {
            _departmentNameService = departmentNameService;
        }

        ///Get All Department Names
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<DepartmentNameDtoGet>>>> GetDepartmentNames([FromQuery] DepartmentNameFilter filter)
        {
            var result = await _departmentNameService.GetDepartmentNamesAsync(filter);
            return Ok(result);
        }

        ///Get Department Name
        [HttpGet]
        public async Task<ActionResult<ApiResponse<DepartmentNameDtoGet>>> GetDepartmentName(int id)
        {
            var result = await _departmentNameService.GetDepartmentAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        ///Create Department Name
        [HttpPost]
        public async Task<ActionResult<ApiResponse<DepartmentNameDtoGet>>> CreateDepartmentName([FromBody] DepartmentNameDtoAdd dto)
        {
            var result = await _departmentNameService.CreateDepartmentAsync(dto);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }
            return CreatedAtAction(nameof(GetDepartmentName), new { id = result.Data.Id }, result);
        }

        ///Update Department Name
        [HttpPut]
        public async Task<ActionResult<ApiResponse<DepartmentNameDtoGet>>> UpdateDepartmentName(int id, [FromBody] DepartmentNameDtoAdd dto)
        {
            var result = await _departmentNameService.UpdateDepartmentAsync(id, dto);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        ///Delete Department Name
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteDepartmentName(int id)
        {
            var result = await _departmentNameService.DeleteDepartmentAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }
    }
}
