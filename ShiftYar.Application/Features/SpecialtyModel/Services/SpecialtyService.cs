using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using ShiftYar.Application.Features.SpecialtyModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.SpecialtyModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.RolePermissionModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System.Security.Claims;

namespace ShiftYar.Application.Features.SpecialtyModel.Services
{
    public class SpecialtyService : ISpecialtyService
    {
        private readonly IEfRepository<Specialty> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<SpecialtyService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SpecialtyService(IEfRepository<Specialty> repository, IMapper mapper, ILogger<SpecialtyService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<SpecialtyDtoGet>>> GetSpecialties(SpecialtyFilter filter)
        {
            try
            {
                _logger.LogInformation("Fetching specialties with filter: {@Filter}", filter);
                var result = await _repository.GetByFilterAsync(filter, "Department", "Department.Hospital", "Department.Supervisor");
                var data = _mapper.Map<List<SpecialtyDtoGet>>(result.Items);

                var pagedResponse = new PagedResponse<SpecialtyDtoGet>
                {
                    Items = data,
                    TotalCount = result.TotalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
                };

                _logger.LogInformation("Successfully fetched {Count} specialties out of {TotalCount}", data.Count, result.TotalCount);
                return ApiResponse<PagedResponse<SpecialtyDtoGet>>.Success(pagedResponse);
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت تخصص ها با خطا مواجه شد : " + ex.Message);
            }
        }


        public async Task<ApiResponse<SpecialtyDtoGet>> GetSpecialty(int id)
        {
            try
            {
                _logger.LogInformation("Fetching specialty with ID: {Id}", id);
                var specialty = await _repository.GetByIdAsync(id, "Department", "Department.Hospital", "Department.Supervisor");
                if (specialty == null)
                {
                    _logger.LogWarning("Specialty with ID {Id} not found", id);
                    return ApiResponse<SpecialtyDtoGet>.Fail("نقش یافت نشد.");
                }

                var dto = _mapper.Map<SpecialtyDtoGet>(specialty);
                _logger.LogInformation("Successfully fetched specialty with ID: {Id}", id);
                return ApiResponse<SpecialtyDtoGet>.Success(dto);
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت تخصص با خطا مواجه شد : " + ex.Message);
            }
        }


        public async Task<ApiResponse<SpecialtyDtoGet>> CreateSpecialty(SpecialtyDtoAdd dto)
        {
            try
            {
                _logger.LogInformation("Creating new specilty with name: {Name}", dto.SpecialtyName);

                var specialty = _mapper.Map<Specialty>(dto);

                specialty.CreateDate = DateTime.Now;
                specialty.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

                await _repository.AddAsync(specialty);
                await _repository.SaveAsync();

                var data = await GetSpecialty(specialty.Id.Value);  //برای اینکه اطلاعات دپارتمان هم در خروجی نمایش داده شود
                var result = _mapper.Map<SpecialtyDtoGet>(specialty);

                _logger.LogInformation("Successfully created specialty with ID: {Id}", specialty.Id);
                return ApiResponse<SpecialtyDtoGet>.Success(result, "تخصص با موفقیت ایجاد شد.");
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس ایجاد تخصص با خطا مواجه شد : " + ex.Message);
            }
        }


        public async Task<ApiResponse<SpecialtyDtoGet>> UpdateSpecialty(int id, SpecialtyDtoAdd dto)
        {
            try
            {
                _logger.LogInformation("Updating specialty with ID: {Id}", id);

                var specialty = await _repository.GetByIdAsync(id);
                if (specialty == null)
                {
                    _logger.LogWarning("Specialty update failed - Specialty with ID {Id} not found", id);
                    return ApiResponse<SpecialtyDtoGet>.Fail("تخصص موردنظر یافت نشد.");
                }

                _mapper.Map(dto, specialty);

                specialty.CreateDate = DateTime.Now;
                specialty.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

                _repository.Update(specialty);
                await _repository.SaveAsync();

                var data = await GetSpecialty(specialty.Id.Value);  //برای اینکه اطلاعات دپارتمان هم در خروجی نمایش داده شود
                var result = _mapper.Map<SpecialtyDtoGet>(specialty);

                _logger.LogInformation("Successfully updated specialty with ID: {Id}", id);
                return ApiResponse<SpecialtyDtoGet>.Success(result, "ویرایش تخصص با موفقیت انجام شد.");
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس ویرایش تخصص با خطا مواجه شد : " + ex.Message);
            }
        }


        public async Task<ApiResponse<string>> DeleteSpecialty(int id)
        {
            try
            {
                _logger.LogInformation("Deleting specialty with ID: {Id}", id);

                var specialty = await _repository.GetByIdAsync(id);
                if (specialty == null)
                {
                    _logger.LogWarning("Specialty deletion failed - Specialty with ID {Id} not found", id);
                    return ApiResponse<string>.Fail("تخصص موردنظر یافت نشد.");
                }

                _repository.Delete(specialty);
                await _repository.SaveAsync();

                _logger.LogInformation("Successfully deleted specialty with ID: {Id}", id);
                return ApiResponse<string>.Success("تخصص موردنظر با موفقیت حذف شد.");
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس حذف تخصص با خطا مواجه شد : " + ex.Message);
            }
        }

    }
}
