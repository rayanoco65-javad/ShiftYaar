using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using ShiftYar.Application.Interfaces.DepartmentModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.DepartmentModel
{
    public class DepartmentController : BaseController
    {
        private readonly IDepartmentService _departmentService;

        public DepartmentController(ShiftYarDbContext context, IDepartmentService departmentService) : base(context)
        {
            _departmentService = departmentService;
        }

        ///Get All Departments
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<DepartmentDtoGet>>>> GetDepartments([FromQuery] DepartmentFilter filter)
        {
            var result = await _departmentService.GetFilteredDepartmentsAsync(filter);
            return Ok(result);
        }

        ///Get Department
        [HttpGet]
        public async Task<ActionResult<ApiResponse<DepartmentDtoGet>>> GetDepartment(int id)
        {
            var result = await _departmentService.GetByIdAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        ///Create Department
        [HttpPost]
        public async Task<ActionResult<ApiResponse<DepartmentDtoGet>>> CreateDepartment([FromBody] DepartmentDtoAdd dto)
        {
            var result = await _departmentService.CreateAsync(dto);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }
            return CreatedAtAction(nameof(GetDepartment), new { id = result.Data.Id }, result);
        }

        ///Update Department
        [HttpPut]
        public async Task<ActionResult<ApiResponse<DepartmentDtoGet>>> UpdateDepartment(int id, [FromBody] DepartmentDtoAdd dto)
        {
            var result = await _departmentService.UpdateAsync(id, dto);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        ///Delete Department
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteDepartment(int id)
        {
            var result = await _departmentService.DeleteAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }
    }
}
