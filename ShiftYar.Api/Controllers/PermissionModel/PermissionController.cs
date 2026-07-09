using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.PermissionModel;
using ShiftYar.Application.Features.PermissionModel.Filters;
using ShiftYar.Application.Interfaces.PermissionModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.PermissionModel
{
    public class PermissionController : BaseController
    {
        private readonly IPermissionService _permissionService;

        public PermissionController(ShiftYarDbContext context, IPermissionService permissionService) : base(context)
        {
            _permissionService = permissionService;
        }

        ///Get All Permissions
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<PermissionDtoGet>>>> GetPermissions([FromQuery] PermissionFilter filter)
        {
            var result = await _permissionService.GetFilteredPermissionsAsync(filter);
            return Ok(result);
        }

        ///Get Permission By Id
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PermissionDtoGet>>> GetPermission(int id)
        {
            var result = await _permissionService.GetByIdAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        ///Create Permission
        [HttpPost]
        public async Task<ActionResult<ApiResponse<PermissionDtoGet>>> CreatePermission([FromBody] PermissionDtoAdd dto)
        {
            var result = await _permissionService.CreateAsync(dto);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }
            return CreatedAtAction(nameof(GetPermission), new { id = result.Data.Id }, result);
        }

        ///Update Permission
        [HttpPut]
        public async Task<ActionResult<ApiResponse<PermissionDtoGet>>> UpdatePermission(int id, [FromBody] PermissionDtoAdd dto)
        {
            var result = await _permissionService.UpdateAsync(id, dto);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        ///Delete Permission
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeletePermission(int id)
        {
            var result = await _permissionService.DeleteAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }
    }
}
