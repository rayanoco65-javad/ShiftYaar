using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Extensions;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.Settings;
using ShiftYar.Application.Features.Settings.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.Settings;
using ShiftYar.Domain.Entities.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.Settings.Services
{
    public class AlgorithmSettingsService : IAlgorithmSettingsService
    {
        private readonly IEfRepository<AlgorithmSettings> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<AlgorithmSettingsService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AlgorithmSettingsService(
            IEfRepository<AlgorithmSettings> repository, 
            IMapper mapper, 
            ILogger<AlgorithmSettingsService> logger, 
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<AlgorithmSettingsDtoGet>>> GetSettingsAsync(AlgorithmSettingsFilter filter)
        {
            try
            {
                var result = await _repository.GetByFilterAsync(filter, "Department");
                var data = _mapper.Map<List<AlgorithmSettingsDtoGet>>(result.Items);

                // تنظیم نام الگوریتم
                foreach (var item in data)
                {
                    item.AlgorithmTypeName = GetAlgorithmTypeName(item.AlgorithmType);
                }

                var paged = new PagedResponse<AlgorithmSettingsDtoGet>
                {
                    Items = data,
                    TotalCount = result.TotalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
                };
                return ApiResponse<PagedResponse<AlgorithmSettingsDtoGet>>.Success(paged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting algorithm settings");
                return ApiResponse<PagedResponse<AlgorithmSettingsDtoGet>>.Fail("خطا در دریافت تنظیمات الگوریتم");
            }
        }

        public async Task<ApiResponse<AlgorithmSettingsDtoGet>> GetSettingAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id, "Department");
                if (entity == null) 
                    return ApiResponse<AlgorithmSettingsDtoGet>.Fail("تنظیمات یافت نشد.");

                var dto = _mapper.Map<AlgorithmSettingsDtoGet>(entity);
                dto.AlgorithmTypeName = GetAlgorithmTypeName(dto.AlgorithmType);
                return ApiResponse<AlgorithmSettingsDtoGet>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting algorithm setting by id {Id}", id);
                return ApiResponse<AlgorithmSettingsDtoGet>.Fail("خطا در دریافت تنظیمات");
            }
        }

        public async Task<ApiResponse<AlgorithmSettingsDtoGet>> GetSettingByDepartmentAndTypeAsync(int? departmentId, int algorithmType)
        {
            try
            {
                var filter = new AlgorithmSettingsFilter
                {
                    DepartmentId = departmentId,
                    AlgorithmType = algorithmType,
                    PageNumber = 1,
                    PageSize = 1
                };

                var result = await _repository.GetByFilterAsync(filter, "Department");
                var entity = result.Items.FirstOrDefault();

                if (entity == null)
                {
                    // اگر تنظیمات مخصوص دپارتمان یافت نشد، تنظیمات سراسری را جست‌وجو کن
                    if (departmentId.HasValue)
                    {
                        filter.DepartmentId = null; // جست‌وجو در تنظیمات سراسری
                        result = await _repository.GetByFilterAsync(filter, "Department");
                        entity = result.Items.FirstOrDefault();
                    }
                }

                if (entity == null)
                    return ApiResponse<AlgorithmSettingsDtoGet>.Fail("تنظیمات الگوریتم یافت نشد.");

                var dto = _mapper.Map<AlgorithmSettingsDtoGet>(entity);
                dto.AlgorithmTypeName = GetAlgorithmTypeName(dto.AlgorithmType);
                return ApiResponse<AlgorithmSettingsDtoGet>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting algorithm setting for department {DepartmentId} and type {AlgorithmType}", departmentId, algorithmType);
                return ApiResponse<AlgorithmSettingsDtoGet>.Fail("خطا در دریافت تنظیمات الگوریتم");
            }
        }

        public async Task<ApiResponse<AlgorithmSettingsDtoGet>> CreateSettingAsync(AlgorithmSettingsDtoAdd dto)
        {
            try
            {
                // بررسی وجود تنظیمات تکراری
                var existingFilter = new AlgorithmSettingsFilter
                {
                    DepartmentId = dto.DepartmentId,
                    AlgorithmType = dto.AlgorithmType,
                    PageNumber = 1,
                    PageSize = 1
                };

                var existing = await _repository.GetByFilterAsync(existingFilter);
                if (existing.Items.Any())
                {
                    var scope = dto.DepartmentId.HasValue ? "این دپارتمان" : "سراسری";
                    return ApiResponse<AlgorithmSettingsDtoGet>.Fail($"برای {scope} و الگوریتم {GetAlgorithmTypeName(dto.AlgorithmType)} قبلاً تنظیمات ثبت شده است.");
                }

                // اعتبارسنجی پارامترها
                if (!ValidateParameters(dto, out var validationMessage))
                {
                    return ApiResponse<AlgorithmSettingsDtoGet>.Fail(validationMessage);
                }

                var entity = _mapper.Map<AlgorithmSettings>(dto);
                entity.CreateDate = DateTime.Now;
                entity.TheUserId = GetCurrentUserId();

                await _repository.AddAsync(entity);
                await _repository.SaveAsync();

                var result = _mapper.Map<AlgorithmSettingsDtoGet>(entity);
                result.AlgorithmTypeName = GetAlgorithmTypeName(result.AlgorithmType);
                return ApiResponse<AlgorithmSettingsDtoGet>.Success(result, "تنظیمات با موفقیت ایجاد شد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating algorithm setting");
                return ApiResponse<AlgorithmSettingsDtoGet>.Fail("خطا در ایجاد تنظیمات");
            }
        }

        public async Task<ApiResponse<AlgorithmSettingsDtoGet>> UpdateSettingAsync(int id, AlgorithmSettingsDtoAdd dto)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null) 
                    return ApiResponse<AlgorithmSettingsDtoGet>.Fail("تنظیمات یافت نشد.");

                // بررسی وجود تنظیمات تکراری (غیر از خودش)
                var existingFilter = new AlgorithmSettingsFilter
                {
                    DepartmentId = dto.DepartmentId,
                    AlgorithmType = dto.AlgorithmType,
                    PageNumber = 1,
                    PageSize = 1000
                };

                var existing = await _repository.GetByFilterAsync(existingFilter);
                if (existing.Items.Any(x => x.Id != id))
                {
                    var scope = dto.DepartmentId.HasValue ? "این دپارتمان" : "سراسری";
                    return ApiResponse<AlgorithmSettingsDtoGet>.Fail($"برای {scope} و الگوریتم {GetAlgorithmTypeName(dto.AlgorithmType)} تنظیمات دیگری وجود دارد.");
                }

                // اعتبارسنجی پارامترها
                if (!ValidateParameters(dto, out var validationMessage))
                {
                    return ApiResponse<AlgorithmSettingsDtoGet>.Fail(validationMessage);
                }

                _mapper.Map(dto, entity);
                entity.UpdateDate = DateTime.Now;
                entity.TheUserId = GetCurrentUserId();

                _repository.Update(entity);
                await _repository.SaveAsync();

                var result = _mapper.Map<AlgorithmSettingsDtoGet>(entity);
                result.AlgorithmTypeName = GetAlgorithmTypeName(result.AlgorithmType);
                return ApiResponse<AlgorithmSettingsDtoGet>.Success(result, "تنظیمات با موفقیت ویرایش شد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating algorithm setting {Id}", id);
                return ApiResponse<AlgorithmSettingsDtoGet>.Fail("خطا در ویرایش تنظیمات");
            }
        }

        public async Task<ApiResponse<string>> DeleteSettingAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null) 
                    return ApiResponse<string>.Fail("تنظیمات یافت نشد.");

                _repository.Delete(entity);
                await _repository.SaveAsync();
                return ApiResponse<string>.Success("تنظیمات با موفقیت حذف شد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting algorithm setting {Id}", id);
                return ApiResponse<string>.Fail("خطا در حذف تنظیمات");
            }
        }

        private bool ValidateParameters(AlgorithmSettingsDtoAdd dto, out string message)
        {
            message = string.Empty;

            // اعتبارسنجی پارامترهای SA
            if (dto.AlgorithmType == 1) // Simulated Annealing
            {
                if (dto.SA_InitialTemperature.HasValue && dto.SA_FinalTemperature.HasValue)
                {
                    if (dto.SA_InitialTemperature <= dto.SA_FinalTemperature)
                    {
                        message = "دمای اولیه باید بزرگتر از دمای نهایی باشد.";
                        return false;
                    }
                }
            }

            return true;
        }

        private string GetAlgorithmTypeName(int algorithmType)
        {
            return algorithmType switch
            {
                1 => "Simulated Annealing",
                2 => "OR-Tools CP-SAT",
                3 => "Hybrid",
                _ => "نامشخص"
            };
        }

        private int GetCurrentUserId()
        {
            try
            {
                //var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
                //return userIdClaim != null ? Convert.ToInt32(userIdClaim.Value) : 0;
                return _httpContextAccessor.HttpContext?.User?.GetUserIdAsInt() ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
