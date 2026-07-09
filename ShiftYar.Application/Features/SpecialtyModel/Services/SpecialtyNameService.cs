using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using ShiftYar.Application.Features.SpecialtyModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.SpecialtyModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.SpecialtyModel.Services
{
    public class SpecialtyNameService : ISpecialtyNameService
    {
        private readonly IEfRepository<SpecialtyName> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<SpecialtyNameService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SpecialtyNameService(IEfRepository<SpecialtyName> repository, IMapper mapper, ILogger<SpecialtyNameService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<SpecialtyNameDtoGet>>> GetSpecialtyNamesAsync(SpecialtyNameFilter filter)
        {
            _logger.LogInformation("Fetching specialty names with filter: {@Filter}", filter);
            var result = await _repository.GetByFilterAsync(filter);
            var data = _mapper.Map<List<SpecialtyNameDtoGet>>(result.Items);

            var pagedResponse = new PagedResponse<SpecialtyNameDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };

            _logger.LogInformation("Successfully fetched {Count} specialty names out of {TotalCount}", data.Count, result.TotalCount);
            return ApiResponse<PagedResponse<SpecialtyNameDtoGet>>.Success(pagedResponse);
        }

        public async Task<ApiResponse<SpecialtyNameDtoGet>> GetSpecialtyNameAsync(int id)
        {
            _logger.LogInformation("Fetching specialty name with ID: {Id}", id);
            var specialtyName = await _repository.GetByIdAsync(id);
            if (specialtyName == null)
            {
                _logger.LogWarning("Specialty name with ID {Id} not found", id);
                return ApiResponse<SpecialtyNameDtoGet>.Fail("نام تخصص یافت نشد.");
            }

            var dto = _mapper.Map<SpecialtyNameDtoGet>(specialtyName);
            _logger.LogInformation("Successfully fetched specialty name with ID: {Id}", id);
            return ApiResponse<SpecialtyNameDtoGet>.Success(dto);
        }

        public async Task<ApiResponse<SpecialtyNameDtoGet>> CreateSpecialtyNameAsync(SpecialtyNameDtoAdd dto)
        {
            _logger.LogInformation("Creating new specialty name with name: {Name}", dto.Name);

            var specialtyName = _mapper.Map<SpecialtyName>(dto);

            specialtyName.CreateDate = DateTime.Now;
            specialtyName.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            await _repository.AddAsync(specialtyName);
            await _repository.SaveAsync();

            var result = _mapper.Map<SpecialtyNameDtoGet>(specialtyName);
            _logger.LogInformation("Successfully created specialty name with ID: {Id}", specialtyName.Id);
            return ApiResponse<SpecialtyNameDtoGet>.Success(result, "نام تخصص با موفقیت ایجاد شد.");
        }

        public async Task<ApiResponse<SpecialtyNameDtoGet>> UpdateSpecialtyNameAsync(int id, SpecialtyNameDtoAdd dto)
        {
            _logger.LogInformation("Updating specialty name with ID: {Id}", id);

            var specialtyName = await _repository.GetByIdAsync(id);
            if (specialtyName == null)
            {
                _logger.LogWarning("Specialty name update failed - Specialty name with ID {Id} not found", id);
                return ApiResponse<SpecialtyNameDtoGet>.Fail("نام تخصص یافت نشد.");
            }

            _mapper.Map(dto, specialtyName);

            specialtyName.UpdateDate = DateTime.Now;
            specialtyName.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            _repository.Update(specialtyName);
            await _repository.SaveAsync();

            var result = _mapper.Map<SpecialtyNameDtoGet>(specialtyName);
            _logger.LogInformation("Successfully updated specialty name with ID: {Id}", id);
            return ApiResponse<SpecialtyNameDtoGet>.Success(result, "ویرایش نام تخصص با موفقیت انجام شد.");
        }

        public async Task<ApiResponse<string>> DeleteSpecialtyNameAsync(int id)
        {
            _logger.LogInformation("Deleting specialty name with ID: {Id}", id);

            var specialtyName = await _repository.GetByIdAsync(id);
            if (specialtyName == null)
            {
                _logger.LogWarning("Specialty name deletion failed - Specialty name with ID {Id} not found", id);
                return ApiResponse<string>.Fail("نام تخصص یافت نشد.");
            }

            _repository.Delete(specialtyName);
            await _repository.SaveAsync();

            _logger.LogInformation("Successfully deleted specialty name with ID: {Id}", id);
            return ApiResponse<string>.Success("نام تخصص با موفقیت حذف شد.");
        }

    }
}
