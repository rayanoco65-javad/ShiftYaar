using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredResponsibilityModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ShiftRequiredResponsibilityModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftRequiredResponsibilityModel.Services
{
    public class ShiftRequiredResponsibilityService : IShiftRequiredResponsibilityService
    {
        private readonly IEfRepository<ShiftRequiredResponsibility> _repository;
        private readonly IEfRepository<Shift> _shiftRepository;
        private readonly IEfRepository<DepartmentResponsibility> _respRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<ShiftRequiredResponsibilityService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ShiftRequiredResponsibilityService(
            IEfRepository<ShiftRequiredResponsibility> repository,
            IEfRepository<Shift> shiftRepository,
            IEfRepository<DepartmentResponsibility> respRepository,
            IMapper mapper,
            ILogger<ShiftRequiredResponsibilityService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _shiftRepository = shiftRepository;
            _respRepository = respRepository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        public async Task<ApiResponse<List<ShiftRequiredResponsibilityDtoGet>>> GetByShiftIdAsync(int shiftId)
        {
            var (items, _) = await _repository.GetByFilterAsync(
                new Common.Filters.SimpleFilter<ShiftRequiredResponsibility>(r => r.ShiftId == shiftId),
                "DepartmentResponsibility");

            var dtos = _mapper.Map<List<ShiftRequiredResponsibilityDtoGet>>(items);
            return ApiResponse<List<ShiftRequiredResponsibilityDtoGet>>.Success(dtos);
        }

        public async Task<ApiResponse<ShiftRequiredResponsibilityDtoGet>> GetByIdAsync(int id)
        {
            var item = await _repository.GetByIdAsync(id, "DepartmentResponsibility");
            if (item == null)
            {
                return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail("نیازمندی مسئولیت یافت نشد.");
            }

            var dto = _mapper.Map<ShiftRequiredResponsibilityDtoGet>(item);
            return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Success(dto);
        }

        public async Task<ApiResponse<ShiftRequiredResponsibilityDtoGet>> CreateAsync(ShiftRequiredResponsibilityDtoAdd dto)
        {
            var validation = await ValidateDtoAsync(dto, null);
            if (!validation.IsValid)
            {
                return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail(validation.Message!);
            }

            var entity = _mapper.Map<ShiftRequiredResponsibility>(dto);
            entity.CreateDate = DateTime.Now;
            entity.TheUserId = GetCurrentUserId();

            await _repository.AddAsync(entity);
            await _repository.SaveAsync();

            var result = await _repository.GetByIdAsync(entity.Id!.Value, "DepartmentResponsibility");
            var resultDto = _mapper.Map<ShiftRequiredResponsibilityDtoGet>(result);
            return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Success(resultDto, "نیازمندی مسئولیت شیفت با موفقیت ثبت شد.");
        }

        public async Task<ApiResponse<ShiftRequiredResponsibilityDtoGet>> UpdateAsync(int id, ShiftRequiredResponsibilityDtoAdd dto)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail("نیازمندی مسئولیت یافت نشد.");
            }

            var validation = await ValidateDtoAsync(dto, id);
            if (!validation.IsValid)
            {
                return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail(validation.Message!);
            }

            _mapper.Map(dto, entity);
            entity.UpdateDate = DateTime.Now;
            entity.TheUserId = GetCurrentUserId();

            _repository.Update(entity);
            await _repository.SaveAsync();

            var result = await _repository.GetByIdAsync(entity.Id!.Value, "DepartmentResponsibility");
            var resultDto = _mapper.Map<ShiftRequiredResponsibilityDtoGet>(result);
            return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Success(resultDto, "نیازمندی مسئولیت شیفت با موفقیت ویرایش شد.");
        }

        public async Task<ApiResponse<string>> DeleteAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                return ApiResponse<string>.Fail("نیازمندی مسئولیت یافت نشد.");
            }

            _repository.Delete(entity);
            await _repository.SaveAsync();
            return ApiResponse<string>.Success(null, "نیازمندی مسئولیت شیفت با موفقیت حذف شد.");
        }

        private async Task<(bool IsValid, string? Message)> ValidateDtoAsync(ShiftRequiredResponsibilityDtoAdd dto, int? currentId)
        {
            var shift = await _shiftRepository.GetByIdAsync(dto.ShiftId, "RequiredSpecialties", "RequiredResponsibilities");
            if (shift == null)
            {
                return (false, "شیفت مربوطه یافت نشد.");
            }

            var resp = await _respRepository.GetByIdAsync(dto.DepartmentResponsibilityId);
            if (resp == null)
            {
                return (false, "مسئولیت مربوطه یافت نشد.");
            }

            // بررسی مطابقت بخش مسئولیت با بخش شیفت
            if (resp.DepartmentId != shift.DepartmentId)
            {
                return (false, "مسئولیت انتخابی متعلق به بخش این شیفت نیست.");
            }

            // ۱. اعتبارسنجی مقادیر روز عادی
            var reqMale = dto.RequiredMaleCount ?? 0;
            var reqFemale = dto.RequiredFemaleCount ?? 0;
            var reqTotal = dto.RequiredTotalCount ?? 0;

            if (reqTotal < (reqMale + reqFemale))
            {
                return (false, "تعداد کل مورد نیاز در روز عادی نمی‌تواند از مجموع تعداد مرد و زن کمتر باشد.");
            }

            // ۲. اعتبارسنجی مقادیر روز تعطیل
            if (dto.HolidayRequiredTotalCount.HasValue)
            {
                var holMale = dto.HolidayRequiredMaleCount ?? 0;
                var holFemale = dto.HolidayRequiredFemaleCount ?? 0;
                var holTotal = dto.HolidayRequiredTotalCount.Value;

                if (holTotal < (holMale + holFemale))
                {
                    return (false, "تعداد کل مورد نیاز در روز تعطیل نمی‌تواند از مجموع تعداد مرد و زن کمتر باشد.");
                }
            }

            // ۳. اعتبارسنجی جمع کل نیازمندی‌های مسئولیت در شیفت با ظرفیت کل شیفت
            var existingOtherResponsibilities = (shift.RequiredResponsibilities ?? Enumerable.Empty<ShiftRequiredResponsibility>())
                .Where(r => !currentId.HasValue || r.Id != currentId.Value)
                .ToList();

            var totalShiftCapacity = (shift.RequiredSpecialties ?? Enumerable.Empty<ShiftRequiredSpecialty>())
                .Sum(s => s.RequiredTottalCount ?? 0);

            if (totalShiftCapacity > 0)
            {
                var sumResponsibilities = existingOtherResponsibilities.Sum(r => r.RequiredTotalCount ?? 0) + reqTotal;
                if (sumResponsibilities > totalShiftCapacity)
                {
                    return (false, $"مجموع نفرات مسئولیت‌ها ({sumResponsibilities} نفر) نمی‌تواند از ظرفیت کل شیفت ({totalShiftCapacity} نفر) بیشتر باشد.");
                }
            }

            return (true, null);
        }
    }
}
