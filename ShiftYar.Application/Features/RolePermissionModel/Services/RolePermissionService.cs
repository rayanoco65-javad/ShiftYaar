using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Application.DTOs.RolePermissionModel;
using ShiftYar.Application.Features.RoleModel.Filters;
using ShiftYar.Application.Features.RoleModel.Services;
using ShiftYar.Application.Features.RolePermissionModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.RolePermissionModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.RoleModel;
using ShiftYar.Domain.Entities.RolePermissionModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.RolePermissionModel.Services
{
    public class RolePermissionService : IRolePermissionService
    {
        private readonly IEfRepository<RolePermission> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<RolePermissionService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RolePermissionService(IEfRepository<RolePermission> repository, IMapper mapper, ILogger<RolePermissionService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<RolePermissionDtoGet>>> GetRolePermissionsAsync(RolePermissionFilter filter)
        {
            _logger.LogInformation("Fetching role permission with filter: {@Filter}", filter);

            var result = await _repository.GetByFilterAsync(filter, "Role", "Permission");
            var data = _mapper.Map<List<RolePermissionDtoGet>>(result.Items);

            var pagedResponse = new PagedResponse<RolePermissionDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };

            _logger.LogInformation("Successfully fetched {Count} role permissions out of {TotalCount}", data.Count, result.TotalCount);
            return ApiResponse<PagedResponse<RolePermissionDtoGet>>.Success(pagedResponse);

        }


        public async Task<ApiResponse<RolePermissionDtoGet>> GetRolePermissionByIdAsync(int id)
        {
            _logger.LogInformation("Fetching role permission with ID: {Id}", id);

            var role = await _repository.GetByIdAsync(id, "Role", "Permission");
            if (role == null)
            {
                _logger.LogWarning("Role permission with ID {Id} not found", id);
                return ApiResponse<RolePermissionDtoGet>.Fail("نقش یافت نشد.");
            }

            var dto = _mapper.Map<RolePermissionDtoGet>(role);
            _logger.LogInformation("Successfully fetched role permission with ID: {Id}", id);
            return ApiResponse<RolePermissionDtoGet>.Success(dto);
        }


        public async Task<ApiResponse<RolePermissionDtoGet>> CreateRolePermissionAsync(RolePermissionDtoAdd dto)
        {
            _logger.LogInformation("Creating new role permission with RoleId: {RoleId}", dto.RoleId);

            var exists = await _repository.ExistsAsync(rp =>
                rp.RoleId == dto.RoleId && rp.PermissionId == dto.PermissionId);

            if (exists)
                return ApiResponse<RolePermissionDtoGet>.Fail("This role permission already exists.");

            var rolePermission = _mapper.Map<RolePermission>(dto);

            rolePermission.CreateDate = DateTime.Now;
            rolePermission.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            await _repository.AddAsync(rolePermission);
            await _repository.SaveAsync();

            var data = await GetRolePermissionByIdAsync(rolePermission.Id.Value);
            var result = _mapper.Map<RolePermissionDtoGet>(rolePermission);

            _logger.LogInformation("Successfully created role permission with ID: {Id}", rolePermission.Id);

            return ApiResponse<RolePermissionDtoGet>.Success(result, "مجوز دسترسی نقش با موفقیت ایجاد شد.");
        }


        public async Task<ApiResponse<RolePermissionDtoGet>> UpdateRolePermissionAsync(int id, RolePermissionDtoAdd dto)
        {
            _logger.LogInformation("Updating role permission with ID: {Id}", id);

            var rolePermission = await _repository.GetByIdAsync(id, "Role", "Permission");
            if (rolePermission == null)
            {
                _logger.LogWarning("Role update failed - Role Permission with ID {Id} not found", id);
                return ApiResponse<RolePermissionDtoGet>.Fail("مجوز دسترسی نقش یافت نشد.");
            }
                
            var exists = await _repository.ExistsAsync(rp =>
                rp.Id != id && rp.RoleId == dto.RoleId && rp.PermissionId == dto.PermissionId);
            if (exists)
            {
                _logger.LogWarning("Role update failed - Role Permission with ID {Id} Exist", id);
                return ApiResponse<RolePermissionDtoGet>.Fail("مجوز دسترسی نقش تکراری است.");
            }

            _mapper.Map(dto, rolePermission);

            rolePermission.UpdateDate = DateTime.Now;
            rolePermission.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            _repository.Update(rolePermission);
            await _repository.SaveAsync();

            var data = await GetRolePermissionByIdAsync(id);
            var result = _mapper.Map<RolePermissionDtoGet>(rolePermission);
            _logger.LogInformation("Successfully updated role permission with ID: {Id}", id);

            return ApiResponse<RolePermissionDtoGet>.Success(result, "ویرایش مجوز دسترسی نقش با موفقیت انجام شد.");
        }


        public async Task<ApiResponse<string>> DeleteRolePermissionAsync(int id)
        {
            _logger.LogInformation("Deleting role permission with ID: {Id}", id);

            var rolePermission = await _repository.GetByIdAsync(id);
            if (rolePermission == null)
            {
                _logger.LogWarning("Role Permission deletion failed - Role Permission with ID {Id} not found", id);
                return ApiResponse<string>.Fail("مجوز دسترسی نقش یافت نشد.");
            }

            _repository.Delete(rolePermission);
            await _repository.SaveAsync();

            _logger.LogInformation("Successfully deleted role permission with ID: {Id}", id);
            return ApiResponse<string>.Success("مجوز دسترسی نقش با موفقیت حذف شد.");
        }
    }
}
