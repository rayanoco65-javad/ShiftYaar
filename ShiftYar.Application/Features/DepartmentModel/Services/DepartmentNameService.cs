using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using ShiftYar.Application.Interfaces.DepartmentModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.DepartmentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.DepartmentModel.Services
{
    public class DepartmentNameService : IDepartmentNameService
    {
        private readonly IEfRepository<DepartmentName> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<DepartmentNameService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DepartmentNameService(IEfRepository<DepartmentName> repository, IMapper mapper, ILogger<DepartmentNameService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<DepartmentNameDtoGet>>> GetDepartmentNamesAsync(DepartmentNameFilter filter)
        {
            _logger.LogInformation("Fetching department names with filter: {@Filter}", filter);
            var result = await _repository.GetByFilterAsync(filter);
            var data = _mapper.Map<List<DepartmentNameDtoGet>>(result.Items);

            var pagedResponse = new PagedResponse<DepartmentNameDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };

            _logger.LogInformation("Successfully fetched {Count} department names out of {TotalCount}", data.Count, result.TotalCount);
            return ApiResponse<PagedResponse<DepartmentNameDtoGet>>.Success(pagedResponse);
        }

        public async Task<ApiResponse<DepartmentNameDtoGet>> GetDepartmentAsync(int id)
        {
            _logger.LogInformation("Fetching department name with ID: {Id}", id);
            var departmentName = await _repository.GetByIdAsync(id);
            if (departmentName == null)
            {
                _logger.LogWarning("Department name with ID {Id} not found", id);
                return ApiResponse<DepartmentNameDtoGet>.Fail("نام دپارتمان یافت نشد.");
            }

            var dto = _mapper.Map<DepartmentNameDtoGet>(departmentName);
            _logger.LogInformation("Successfully fetched department name with ID: {Id}", id);
            return ApiResponse<DepartmentNameDtoGet>.Success(dto);
        }

        public async Task<ApiResponse<DepartmentNameDtoGet>> CreateDepartmentAsync(DepartmentNameDtoAdd dto)
        {
            _logger.LogInformation("Creating new department name with name: {Name}", dto.Name);

            var departmentName = _mapper.Map<DepartmentName>(dto);

            departmentName.CreateDate = DateTime.Now;
            departmentName.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            await _repository.AddAsync(departmentName);
            await _repository.SaveAsync();

            var result = _mapper.Map<DepartmentNameDtoGet>(departmentName);
            _logger.LogInformation("Successfully created department name with ID: {Id}", departmentName.Id);
            return ApiResponse<DepartmentNameDtoGet>.Success(result, "نام دپارتمان با موفقیت ایجاد شد.");
        }

        public async Task<ApiResponse<DepartmentNameDtoGet>> UpdateDepartmentAsync(int id, DepartmentNameDtoAdd dto)
        {
            _logger.LogInformation("Updating department name with ID: {Id}", id);

            var departmentName = await _repository.GetByIdAsync(id);
            if (departmentName == null)
            {
                _logger.LogWarning("Department name update failed - Department name with ID {Id} not found", id);
                return ApiResponse<DepartmentNameDtoGet>.Fail("نام دپارتمان یافت نشد.");
            }

            _mapper.Map(dto, departmentName);

            departmentName.UpdateDate = DateTime.Now;
            departmentName.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            _repository.Update(departmentName);
            await _repository.SaveAsync();

            var result = _mapper.Map<DepartmentNameDtoGet>(departmentName);
            _logger.LogInformation("Successfully updated department name with ID: {Id}", id);
            return ApiResponse<DepartmentNameDtoGet>.Success(result, "ویرایش نام دپارتمان با موفقیت انجام شد.");
        }

        public async Task<ApiResponse<string>> DeleteDepartmentAsync(int id)
        {
            _logger.LogInformation("Deleting department name with ID: {Id}", id);

            var departmentName = await _repository.GetByIdAsync(id);
            if (departmentName == null)
            {
                _logger.LogWarning("Department name deletion failed - Department name with ID {Id} not found", id);
                return ApiResponse<string>.Fail("نام دپارتمان یافت نشد.");
            }

            _repository.Delete(departmentName);
            await _repository.SaveAsync();

            _logger.LogInformation("Successfully deleted department name with ID: {Id}", id);
            return ApiResponse<string>.Success("نام دپارتمان با موفقیت حذف شد.");
        }
    }
}
