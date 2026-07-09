using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Application.Features.RoleModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.RoleModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.RoleModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.RoleModel.Services
{
    public class RoleService : IRoleService
    {
        private readonly IEfRepository<Role> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<RoleService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RoleService(IEfRepository<Role> repository, IMapper mapper, ILogger<RoleService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<RoleDtoGet>>> GetFilteredRolesAsync(RoleFilter filter)
        {
            _logger.LogInformation("Fetching roles with filter: {@Filter}", filter);
            var result = await _repository.GetByFilterAsync(filter);
            var data = _mapper.Map<List<RoleDtoGet>>(result.Items);

            var pagedResponse = new PagedResponse<RoleDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };

            _logger.LogInformation("Successfully fetched {Count} roles out of {TotalCount}", data.Count, result.TotalCount);
            return ApiResponse<PagedResponse<RoleDtoGet>>.Success(pagedResponse);
        }


        public async Task<ApiResponse<RoleDtoGet>> GetByIdAsync(int id)
        {
            _logger.LogInformation("Fetching role with ID: {Id}", id);
            var role = await _repository.GetByIdAsync(id);
            if (role == null)
            {
                _logger.LogWarning("Role with ID {Id} not found", id);
                return ApiResponse<RoleDtoGet>.Fail("نقش یافت نشد.");
            }

            var dto = _mapper.Map<RoleDtoGet>(role);
            _logger.LogInformation("Successfully fetched role with ID: {Id}", id);
            return ApiResponse<RoleDtoGet>.Success(dto);
        }


        public async Task<ApiResponse<RoleDtoGet>> CreateAsync(RoleDtoAdd dto)
        {
            _logger.LogInformation("Creating new role with name: {Name}", dto.Name);

            var role = _mapper.Map<Role>(dto);

            role.CreateDate = DateTime.Now;
            role.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            await _repository.AddAsync(role);
            await _repository.SaveAsync();

            var result = _mapper.Map<RoleDtoGet>(role);
            _logger.LogInformation("Successfully created role with ID: {Id}", role.Id);
            return ApiResponse<RoleDtoGet>.Success(result, "نقش با موفقیت ایجاد شد.");
        }


        public async Task<ApiResponse<RoleDtoGet>> UpdateAsync(int id, RoleDtoAdd dto)
        {
            _logger.LogInformation("Updating role with ID: {Id}", id);

            var role = await _repository.GetByIdAsync(id);
            if (role == null)
            {
                _logger.LogWarning("Role update failed - Role with ID {Id} not found", id);
                return ApiResponse<RoleDtoGet>.Fail("نقش یافت نشد.");
            }

            _mapper.Map(dto, role);

            role.UpdateDate = DateTime.Now;
            role.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            _repository.Update(role);
            await _repository.SaveAsync();

            var result = _mapper.Map<RoleDtoGet>(role);
            _logger.LogInformation("Successfully updated role with ID: {Id}", id);
            return ApiResponse<RoleDtoGet>.Success(result, "ویرایش نقش با موفقیت انجام شد.");
        }


        public async Task<ApiResponse<string>> DeleteAsync(int id)
        {
            _logger.LogInformation("Deleting role with ID: {Id}", id);

            var role = await _repository.GetByIdAsync(id);
            if (role == null)
            {
                _logger.LogWarning("Role deletion failed - Role with ID {Id} not found", id);
                return ApiResponse<string>.Fail("نقش یافت نشد.");
            }

            _repository.Delete(role);
            await _repository.SaveAsync();

            _logger.LogInformation("Successfully deleted role with ID: {Id}", id);
            return ApiResponse<string>.Success("نقش با موفقیت حذف شد.");
        }
    }
}