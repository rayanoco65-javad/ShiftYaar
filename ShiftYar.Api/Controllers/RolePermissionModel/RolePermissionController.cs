using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Application.DTOs.RolePermissionModel;
using ShiftYar.Application.Features.RoleModel.Filters;
using ShiftYar.Application.Features.RolePermissionModel.Filters;
using ShiftYar.Application.Interfaces.RolePermissionModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.RolePermissionModel
{
    public class RolePermissionController : BaseController
    {
        private readonly IRolePermissionService _rolePermissionService;
        public RolePermissionController(ShiftYarDbContext context, IRolePermissionService rolePermissionService) : base(context)
        {
            _rolePermissionService = rolePermissionService;
        }

        ///Get All Role Permissions
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<RolePermissionDtoGet>>>> GetRolePermissions([FromQuery] RolePermissionFilter filter)
        {
            var result = await _rolePermissionService.GetRolePermissionsAsync(filter);
            return Ok(result);
        }


        ///Get Role Permission By Id
        [HttpGet]
        public async Task<ActionResult<ApiResponse<RolePermissionDtoGet>>> GetRolePermission(int id)
        {
            var result = await _rolePermissionService.GetRolePermissionByIdAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }


        ///Create Role Permission
        [HttpPost]
        public async Task<ActionResult<ApiResponse<RolePermissionDtoGet>>> CreateRolePermission([FromBody] RolePermissionDtoAdd dto)
        {
            var result = await _rolePermissionService.CreateRolePermissionAsync(dto);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }
            return CreatedAtAction(nameof(GetRolePermission), new { id = result.Data.Id }, result);
        }


        ///Update Role Permission
        [HttpPut]
        public async Task<ActionResult<ApiResponse<RolePermissionDtoGet>>> UpdateRolePermission(int id, [FromBody] RolePermissionDtoAdd dto)
        {
            var result = await _rolePermissionService.UpdateRolePermissionAsync(id, dto);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }


        ///Delete Role Permission
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> DeleteRolePermission(int id)
        {
            var result = await _rolePermissionService.DeleteRolePermissionAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(result);
            }
            return Ok(result);
        }
    }
}
