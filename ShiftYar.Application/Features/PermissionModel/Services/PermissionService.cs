using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.PermissionModel;
using ShiftYar.Application.Features.PermissionModel.Filters;
using ShiftYar.Application.Interfaces.PermissionModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.PermissionModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.PermissionModel.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IEfRepository<Permission> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<PermissionService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PermissionService(IEfRepository<Permission> repository, IMapper mapper, ILogger<PermissionService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<PermissionDtoGet>>> GetFilteredPermissionsAsync(PermissionFilter filter)
        {
            _logger.LogInformation("Fetching permissions with filter: {@Filter}", filter);
            var result = await _repository.GetByFilterAsync(filter);
            var data = _mapper.Map<List<PermissionDtoGet>>(result.Items);

            var pagedResponse = new PagedResponse<PermissionDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };

            _logger.LogInformation("Successfully fetched {Count} permissions out of {TotalCount}", data.Count, result.TotalCount);
            return ApiResponse<PagedResponse<PermissionDtoGet>>.Success(pagedResponse);
        }

        public async Task<ApiResponse<PermissionDtoGet>> GetByIdAsync(int id)
        {
            _logger.LogInformation("Fetching permission with ID: {Id}", id);
            var permission = await _repository.GetByIdAsync(id);
            if (permission == null)
            {
                _logger.LogWarning("Permission with ID {Id} not found", id);
                return ApiResponse<PermissionDtoGet>.Fail("دسترسی یافت نشد.");
            }

            var dto = _mapper.Map<PermissionDtoGet>(permission);
            _logger.LogInformation("Successfully fetched permission with ID: {Id}", id);
            return ApiResponse<PermissionDtoGet>.Success(dto);
        }

        public async Task<ApiResponse<PermissionDtoGet>> CreateAsync(PermissionDtoAdd dto)
        {
            _logger.LogInformation("Creating new permission with name: {Name}", dto.Name);

            var permission = _mapper.Map<Permission>(dto);

            permission.CreateDate = DateTime.Now;
            permission.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            await _repository.AddAsync(permission);
            await _repository.SaveAsync();

            var result = _mapper.Map<PermissionDtoGet>(permission);
            _logger.LogInformation("Successfully created permission with ID: {Id}", permission.Id);
            return ApiResponse<PermissionDtoGet>.Success(result, "دسترسی با موفقیت ایجاد شد.");
        }

        public async Task<ApiResponse<PermissionDtoGet>> UpdateAsync(int id, PermissionDtoAdd dto)
        {
            _logger.LogInformation("Updating permission with ID: {Id}", id);

            var permission = await _repository.GetByIdAsync(id);
            if (permission == null)
            {
                _logger.LogWarning("Permission update failed - Permission with ID {Id} not found", id);
                return ApiResponse<PermissionDtoGet>.Fail("دسترسی یافت نشد.");
            }

            _mapper.Map(dto, permission);

            permission.UpdateDate = DateTime.Now;
            permission.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            _repository.Update(permission);
            await _repository.SaveAsync();

            var result = _mapper.Map<PermissionDtoGet>(permission);
            _logger.LogInformation("Successfully updated permission with ID: {Id}", id);
            return ApiResponse<PermissionDtoGet>.Success(result, "ویرایش دسترسی با موفقیت انجام شد.");
        }

        public async Task<ApiResponse<string>> DeleteAsync(int id)
        {
            _logger.LogInformation("Deleting permission with ID: {Id}", id);

            var permission = await _repository.GetByIdAsync(id);
            if (permission == null)
            {
                _logger.LogWarning("Permission deletion failed - Permission with ID {Id} not found", id);
                return ApiResponse<string>.Fail("دسترسی یافت نشد.");
            }

            _repository.Delete(permission);
            await _repository.SaveAsync();

            _logger.LogInformation("Successfully deleted permission with ID: {Id}", id);
            return ApiResponse<string>.Success("دسترسی با موفقیت حذف شد.");
        }
    }
}
