using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredSpecialtyModel;
using ShiftYar.Application.Features.ShiftRequiredSpecialtyModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ShiftRequiredSpecialtyModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftRequiredSpecialtyModel.Services
{
    public class ShiftRequiredSpecialtyService : IShiftRequiredSpecialtyService
    {
        private readonly IEfRepository<ShiftRequiredSpecialty> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<ShiftRequiredSpecialtyService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ShiftRequiredSpecialtyService(IEfRepository<ShiftRequiredSpecialty> repository, IMapper mapper, ILogger<ShiftRequiredSpecialtyService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<ShiftRequiredSpecialtyDtoGet>>> GetShiftRequiredSpecialties(ShiftRequiredSpecialtyFilter filter)
        {
            try
            {
                _logger.LogInformation("Fetching shift required specialties with filter: {@Filter}", filter);
                var result = await _repository.GetByFilterAsync(filter, "Specialty", "Shift", "Shift.Department");
                var data = _mapper.Map<List<ShiftRequiredSpecialtyDtoGet>>(result.Items);

                var pagedResponse = new PagedResponse<ShiftRequiredSpecialtyDtoGet>
                {
                    Items = data,
                    TotalCount = result.TotalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
                };

                _logger.LogInformation("Successfully fetched {Count} shift required specialties out of {TotalCount}", data.Count, result.TotalCount);
                return ApiResponse<PagedResponse<ShiftRequiredSpecialtyDtoGet>>.Success(pagedResponse);
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت نیازمندیهای شیفت با خطا مواجه شد : " + ex.Message);
            }
        }

        public async Task<ApiResponse<ShiftRequiredSpecialtyDtoGet>> GetShiftRequiredSpecialty(int id)
        {
            try
            {
                _logger.LogInformation("Fetching shift required specialty with ID: {Id}", id);
                var shiftRequiredSpecialty = await _repository.GetByIdAsync(id, "Specialty", "Shift");
                if (shiftRequiredSpecialty == null)
                {
                    _logger.LogWarning("Shift required specialty with ID {Id} not found", id);
                    return ApiResponse<ShiftRequiredSpecialtyDtoGet>.Fail("تخصص مورد نیاز شیفت یافت نشد.");
                }

                var dto = _mapper.Map<ShiftRequiredSpecialtyDtoGet>(shiftRequiredSpecialty);
                _logger.LogInformation("Successfully fetched shift required specialty with ID: {Id}", id);
                return ApiResponse<ShiftRequiredSpecialtyDtoGet>.Success(dto);
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت نیازمندی شیفت با خطا مواجه شد : " + ex.Message);
            }
        }

        public async Task<ApiResponse<ShiftRequiredSpecialtyDtoGet>> CreateShiftRequiredSpecialty(ShiftRequiredSpecialtyDtoAdd dto)
        {
            try
            {
                _logger.LogInformation("Creating new shift required specialty");

                var shiftRequiredSpecialty = _mapper.Map<ShiftRequiredSpecialty>(dto);

                shiftRequiredSpecialty.CreateDate = DateTime.Now;
                shiftRequiredSpecialty.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

                await _repository.AddAsync(shiftRequiredSpecialty);
                await _repository.SaveAsync();

                var result = _mapper.Map<ShiftRequiredSpecialtyDtoGet>(shiftRequiredSpecialty);
                _logger.LogInformation("Successfully created shift required specialty with ID: {Id}", shiftRequiredSpecialty.Id);
                return ApiResponse<ShiftRequiredSpecialtyDtoGet>.Success(result, "نیازمندی شیفت با موفقیت ایجاد شد.");
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس ایجاد نیازمندی تخصص شیفت با خطا مواجه شد : " + ex.Message);
            }
        }

        public async Task<ApiResponse<ShiftRequiredSpecialtyDtoGet>> UpdateShiftRequiredSpecialty(int id, ShiftRequiredSpecialtyDtoAdd dto)
        {
            _logger.LogInformation("Updating shift required specialty with ID: {Id}", id);

            var shiftRequiredSpecialty = await _repository.GetByIdAsync(id);
            if (shiftRequiredSpecialty == null)
            {
                _logger.LogWarning("Shift required specialty update failed - Shift required specialty with ID {Id} not found", id);
                return ApiResponse<ShiftRequiredSpecialtyDtoGet>.Fail("نیازمندی تخصص شیفت یافت نشد.");
            }

            _mapper.Map(dto, shiftRequiredSpecialty);

            shiftRequiredSpecialty.UpdateDate = DateTime.Now;
            shiftRequiredSpecialty.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            _repository.Update(shiftRequiredSpecialty);
            await _repository.SaveAsync();

            var result = _mapper.Map<ShiftRequiredSpecialtyDtoGet>(shiftRequiredSpecialty);
            _logger.LogInformation("Successfully updated shift required specialty with ID: {Id}", id);
            return ApiResponse<ShiftRequiredSpecialtyDtoGet>.Success(result, "نیازمندی تخصص شیفت با موفقیت بروزرسانی شد.");
        }

        public async Task<ApiResponse<string>> DeleteShiftRequiredSpecialty(int id)
        {
            _logger.LogInformation("Deleting shift required specialty with ID: {Id}", id);

            var shiftRequiredSpecialty = await _repository.GetByIdAsync(id);
            if (shiftRequiredSpecialty == null)
            {
                _logger.LogWarning("Shift required specialty deletion failed - Shift required specialty with ID {Id} not found", id);
                return ApiResponse<string>.Fail("نیازمندی تخصص شیفت یافت نشد.");
            }

            _repository.Delete(shiftRequiredSpecialty);
            await _repository.SaveAsync();

            _logger.LogInformation("Successfully deleted shift required specialty with ID: {Id}", id);
            return ApiResponse<string>.Success("نیازمندی تخصص شیفت با موفقیت حذف شد.");
        }
    }
}
