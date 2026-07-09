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
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.DepartmentModel.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly IEfRepository<Department> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<DepartmentService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DepartmentService(IEfRepository<Department> repository, IMapper mapper, ILogger<DepartmentService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<DepartmentDtoGet>>> GetFilteredDepartmentsAsync(DepartmentFilter filter)
        {
            _logger.LogInformation("Fetching departments with filter: {@Filter}", filter);
            var result = await _repository.GetByFilterAsync(filter, "Hospital", "Supervisor", "DepartmentUsers");
            var data = _mapper.Map<List<DepartmentDtoGet>>(result.Items);

            var pagedResponse = new PagedResponse<DepartmentDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };

            _logger.LogInformation("Successfully fetched {Count} departments out of {TotalCount}", data.Count, result.TotalCount);
            return ApiResponse<PagedResponse<DepartmentDtoGet>>.Success(pagedResponse);
        }

        public async Task<ApiResponse<DepartmentDtoGet>> GetByIdAsync(int id)
        {
            _logger.LogInformation("Fetching department with ID: {Id}", id);
            var department = await _repository.GetByIdAsync(id, "Hospital", "Supervisor", "DepartmentUsers");
            if (department == null)
            {
                _logger.LogWarning("Department with ID {Id} not found", id);
                return ApiResponse<DepartmentDtoGet>.Fail("دپارتمان یافت نشد.");
            }

            var dto = _mapper.Map<DepartmentDtoGet>(department);
            _logger.LogInformation("Successfully fetched department with ID: {Id}", id);
            return ApiResponse<DepartmentDtoGet>.Success(dto);
        }

        public async Task<ApiResponse<DepartmentDtoGet>> CreateAsync(DepartmentDtoAdd dto)
        {
            _logger.LogInformation("Creating new department with name: {Name}", dto.Name);

            var department = _mapper.Map<Department>(dto);

            department.CreateDate = DateTime.Now;
            department.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            await _repository.AddAsync(department);
            await _repository.SaveAsync();

            var result = _mapper.Map<DepartmentDtoGet>(department);
            _logger.LogInformation("Successfully created department with ID: {Id}", department.Id);
            return ApiResponse<DepartmentDtoGet>.Success(result, "دپارتمان با موفقیت ایجاد شد.");
        }

        public async Task<ApiResponse<DepartmentDtoGet>> UpdateAsync(int id, DepartmentDtoAdd dto)
        {
            _logger.LogInformation("Updating department with ID: {Id}", id);

            var department = await _repository.GetByIdAsync(id);
            if (department == null)
            {
                _logger.LogWarning("Department update failed - Department with ID {Id} not found", id);
                return ApiResponse<DepartmentDtoGet>.Fail("دپارتمان یافت نشد.");
            }

            _mapper.Map(dto, department);

            department.UpdateDate = DateTime.Now;
            department.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            _repository.Update(department);
            await _repository.SaveAsync();

            var result = _mapper.Map<DepartmentDtoGet>(department);
            _logger.LogInformation("Successfully updated department with ID: {Id}", id);
            return ApiResponse<DepartmentDtoGet>.Success(result, "ویرایش دپارتمان با موفقیت انجام شد.");
        }

        public async Task<ApiResponse<string>> DeleteAsync(int id)
        {
            _logger.LogInformation("Deleting department with ID: {Id}", id);

            var department = await _repository.GetByIdAsync(id);
            if (department == null)
            {
                _logger.LogWarning("Department deletion failed - Department with ID {Id} not found", id);
                return ApiResponse<string>.Fail("دپارتمان یافت نشد.");
            }

            _repository.Delete(department);
            await _repository.SaveAsync();

            _logger.LogInformation("Successfully deleted department with ID: {Id}", id);
            return ApiResponse<string>.Success("دپارتمان با موفقیت حذف شد.");
        }
    }
}