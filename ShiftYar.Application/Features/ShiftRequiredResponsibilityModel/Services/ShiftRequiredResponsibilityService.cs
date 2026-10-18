using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredResponsibilityModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ShiftRequiredResponsibilityModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
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
        private readonly IEfRepository<ShiftRequiredSpecialty> _specialtyReqRepository;
        private readonly IEfRepository<Shift> _shiftRepository;
        private readonly IEfRepository<DepartmentResponsibility> _respRepository;
        private readonly IEfRepository<User> _userRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<ShiftRequiredResponsibilityService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ShiftRequiredResponsibilityService(
            IEfRepository<ShiftRequiredResponsibility> repository,
            IEfRepository<ShiftRequiredSpecialty> specialtyReqRepository,
            IEfRepository<Shift> shiftRepository,
            IEfRepository<DepartmentResponsibility> respRepository,
            IEfRepository<User> userRepository,
            IMapper mapper,
            ILogger<ShiftRequiredResponsibilityService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _specialtyReqRepository = specialtyReqRepository;
            _shiftRepository = shiftRepository;
            _respRepository = respRepository;
            _userRepository = userRepository;
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

            // اگر در جدول ShiftRequiredResponsibilities هنوز رکوردی ثبت نشده بود اما در ShiftRequiredSpecialties با شناسه نقش موجود بود
            if (dtos.Count == 0)
            {
                var (specialties, _) = await _specialtyReqRepository.GetByFilterAsync(
                    new Common.Filters.SimpleFilter<ShiftRequiredSpecialty>(s => s.ShiftId == shiftId && s.DepartmentResponsibilityId.HasValue),
                    "DepartmentResponsibility");

                dtos = specialties.Select(s => new ShiftRequiredResponsibilityDtoGet
                {
                    Id = s.Id ?? 0,
                    ShiftId = s.ShiftId ?? shiftId,
                    DepartmentResponsibilityId = s.DepartmentResponsibilityId ?? 0,
                    ResponsibilityTitle = s.DepartmentResponsibility?.Title ?? "",
                    IsDefaultResponsibility = s.DepartmentResponsibility?.IsDefault ?? false,
                    RequiredMaleCount = s.RequiredMaleCount,
                    RequiredFemaleCount = s.RequiredFemaleCount,
                    RequiredTotalCount = s.RequiredTottalCount,
                    HolidayRequiredMaleCount = s.HolidayRequiredMaleCount,
                    HolidayRequiredFemaleCount = s.HolidayRequiredFemaleCount,
                    HolidayRequiredTotalCount = s.HolidayRequiredTottalCount
                }).ToList();
            }

            return ApiResponse<List<ShiftRequiredResponsibilityDtoGet>>.Success(dtos);
        }

        public async Task<ApiResponse<ShiftRequiredResponsibilityDtoGet>> GetByIdAsync(int id)
        {
            var item = await _repository.GetByIdAsync(id, "DepartmentResponsibility");
            if (item != null)
            {
                var dto = _mapper.Map<ShiftRequiredResponsibilityDtoGet>(item);
                return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Success(dto);
            }

            // جستجو در جدول ShiftRequiredSpecialties
            var specItem = await _specialtyReqRepository.GetByIdAsync(id, "DepartmentResponsibility");
            if (specItem != null && specItem.DepartmentResponsibilityId.HasValue)
            {
                var dto = new ShiftRequiredResponsibilityDtoGet
                {
                    Id = specItem.Id ?? id,
                    ShiftId = specItem.ShiftId ?? 0,
                    DepartmentResponsibilityId = specItem.DepartmentResponsibilityId.Value,
                    ResponsibilityTitle = specItem.DepartmentResponsibility?.Title ?? "",
                    IsDefaultResponsibility = specItem.DepartmentResponsibility?.IsDefault ?? false,
                    RequiredMaleCount = specItem.RequiredMaleCount,
                    RequiredFemaleCount = specItem.RequiredFemaleCount,
                    RequiredTotalCount = specItem.RequiredTottalCount,
                    HolidayRequiredMaleCount = specItem.HolidayRequiredMaleCount,
                    HolidayRequiredFemaleCount = specItem.HolidayRequiredFemaleCount,
                    HolidayRequiredTotalCount = specItem.HolidayRequiredTottalCount
                };
                return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Success(dto);
            }

            return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail("نیازمندی مسئولیت یافت نشد.");
        }

        public async Task<ApiResponse<ShiftRequiredResponsibilityDtoGet>> CreateAsync(ShiftRequiredResponsibilityDtoAdd dto)
        {
            var validation = await ValidateDtoAsync(dto, null);
            if (!validation.IsValid)
            {
                return ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail(validation.Message!);
            }

            var currentUserId = GetCurrentUserId();

            // ۱. ثبت در جدول ShiftRequiredResponsibilities
            var entity = _mapper.Map<ShiftRequiredResponsibility>(dto);
            entity.CreateDate = DateTime.Now;
            entity.TheUserId = currentUserId;

            await _repository.AddAsync(entity);
            await _repository.SaveAsync();

            // ۲. استخراج تخصص پیش‌فرض نقش یا بخش جهت انتساب به تخصص نیازمندی شیفت
            var shift = await _shiftRepository.GetByIdAsync(dto.ShiftId);
            var resp = await _respRepository.GetByIdAsync(dto.DepartmentResponsibilityId);
            int? resolvedSpecialtyId = resp?.SpecialtyId;
            if (!resolvedSpecialtyId.HasValue && shift?.DepartmentId != null)
            {
                var (deptUsers, _) = await _userRepository.GetByFilterAsync(
                    new Common.Filters.SimpleFilter<User>(u => u.DepartmentId == shift.DepartmentId && u.SpecialtyId.HasValue && (u.IsActive ?? true)));
                resolvedSpecialtyId = deptUsers.FirstOrDefault()?.SpecialtyId;
            }

            // ۳. همگام‌سازی و ذخیره در جدول اصلی ShiftRequiredSpecialties تا کل موتور زمان‌بندی و اعتبارسنجی‌ها از آن بخوانند
            var (existingSpecialties, _) = await _specialtyReqRepository.GetByFilterAsync(
                new Common.Filters.SimpleFilter<ShiftRequiredSpecialty>(s =>
                    s.ShiftId == dto.ShiftId && s.DepartmentResponsibilityId == dto.DepartmentResponsibilityId));

            var specRecord = existingSpecialties.FirstOrDefault();
            if (specRecord == null)
            {
                specRecord = new ShiftRequiredSpecialty
                {
                    ShiftId = dto.ShiftId,
                    DepartmentResponsibilityId = dto.DepartmentResponsibilityId,
                    SpecialtyId = resolvedSpecialtyId,
                    RequiredMaleCount = dto.RequiredMaleCount,
                    RequiredFemaleCount = dto.RequiredFemaleCount,
                    RequiredTottalCount = dto.RequiredTotalCount,
                    HolidayRequiredMaleCount = dto.HolidayRequiredMaleCount,
                    HolidayRequiredFemaleCount = dto.HolidayRequiredFemaleCount,
                    HolidayRequiredTottalCount = dto.HolidayRequiredTotalCount,
                    CreateDate = DateTime.Now,
                    TheUserId = currentUserId
                };
                await _specialtyReqRepository.AddAsync(specRecord);
            }
            else
            {
                specRecord.RequiredMaleCount = dto.RequiredMaleCount;
                specRecord.RequiredFemaleCount = dto.RequiredFemaleCount;
                specRecord.RequiredTottalCount = dto.RequiredTotalCount;
                specRecord.HolidayRequiredMaleCount = dto.HolidayRequiredMaleCount;
                specRecord.HolidayRequiredFemaleCount = dto.HolidayRequiredFemaleCount;
                specRecord.HolidayRequiredTottalCount = dto.HolidayRequiredTotalCount;
                if (!specRecord.SpecialtyId.HasValue && resolvedSpecialtyId.HasValue)
                {
                    specRecord.SpecialtyId = resolvedSpecialtyId;
                }
                specRecord.UpdateDate = DateTime.Now;
                specRecord.TheUserId = currentUserId;
                _specialtyReqRepository.Update(specRecord);
            }
            await _specialtyReqRepository.SaveAsync();

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

            var previousShiftId = entity.ShiftId;
            var previousRespId = entity.DepartmentResponsibilityId;
            var currentUserId = GetCurrentUserId();

            _mapper.Map(dto, entity);
            entity.UpdateDate = DateTime.Now;
            entity.TheUserId = currentUserId;

            _repository.Update(entity);
            await _repository.SaveAsync();

            // به‌روزرسانی هم‌زمان در ShiftRequiredSpecialties
            var (existingSpecialties, _) = await _specialtyReqRepository.GetByFilterAsync(
                new Common.Filters.SimpleFilter<ShiftRequiredSpecialty>(s =>
                    (s.ShiftId == entity.ShiftId && s.DepartmentResponsibilityId == entity.DepartmentResponsibilityId) ||
                    (previousShiftId.HasValue && previousRespId.HasValue && s.ShiftId == previousShiftId && s.DepartmentResponsibilityId == previousRespId)));

            var specRecord = existingSpecialties.FirstOrDefault();
            if (specRecord != null)
            {
                specRecord.ShiftId = entity.ShiftId;
                specRecord.DepartmentResponsibilityId = entity.DepartmentResponsibilityId;
                specRecord.RequiredMaleCount = entity.RequiredMaleCount;
                specRecord.RequiredFemaleCount = entity.RequiredFemaleCount;
                specRecord.RequiredTottalCount = entity.RequiredTotalCount;
                specRecord.HolidayRequiredMaleCount = entity.HolidayRequiredMaleCount;
                specRecord.HolidayRequiredFemaleCount = entity.HolidayRequiredFemaleCount;
                specRecord.HolidayRequiredTottalCount = entity.HolidayRequiredTotalCount;
                specRecord.UpdateDate = DateTime.Now;
                specRecord.TheUserId = currentUserId;
                _specialtyReqRepository.Update(specRecord);
                await _specialtyReqRepository.SaveAsync();
            }
            else
            {
                var shift = await _shiftRepository.GetByIdAsync(entity.ShiftId ?? 0);
                var resp = await _respRepository.GetByIdAsync(entity.DepartmentResponsibilityId ?? 0);
                int? resolvedSpecialtyId = resp?.SpecialtyId;
                if (!resolvedSpecialtyId.HasValue && shift?.DepartmentId != null)
                {
                    var (deptUsers, _) = await _userRepository.GetByFilterAsync(
                        new Common.Filters.SimpleFilter<User>(u => u.DepartmentId == shift.DepartmentId && u.SpecialtyId.HasValue && (u.IsActive ?? true)));
                    resolvedSpecialtyId = deptUsers.FirstOrDefault()?.SpecialtyId;
                }

                specRecord = new ShiftRequiredSpecialty
                {
                    ShiftId = entity.ShiftId,
                    DepartmentResponsibilityId = entity.DepartmentResponsibilityId,
                    SpecialtyId = resolvedSpecialtyId,
                    RequiredMaleCount = entity.RequiredMaleCount,
                    RequiredFemaleCount = entity.RequiredFemaleCount,
                    RequiredTottalCount = entity.RequiredTotalCount,
                    HolidayRequiredMaleCount = entity.HolidayRequiredMaleCount,
                    HolidayRequiredFemaleCount = entity.HolidayRequiredFemaleCount,
                    HolidayRequiredTottalCount = entity.HolidayRequiredTotalCount,
                    CreateDate = DateTime.Now,
                    TheUserId = currentUserId
                };
                await _specialtyReqRepository.AddAsync(specRecord);
                await _specialtyReqRepository.SaveAsync();
            }

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

            // حذف هم‌زمان از ShiftRequiredSpecialties
            if (entity.ShiftId.HasValue && entity.DepartmentResponsibilityId.HasValue)
            {
                var (existingSpecialties, _) = await _specialtyReqRepository.GetByFilterAsync(
                    new Common.Filters.SimpleFilter<ShiftRequiredSpecialty>(s =>
                        s.ShiftId == entity.ShiftId && s.DepartmentResponsibilityId == entity.DepartmentResponsibilityId));

                foreach (var spec in existingSpecialties)
                {
                    _specialtyReqRepository.Delete(spec);
                }
                await _specialtyReqRepository.SaveAsync();
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

            // ۳. اعتبارسنجی جمع کل نیازمندی‌های مسئولیت در شیفت با ظرفیت کل شیفت:
            // اگر ظرفیت بر اساس تخصص‌های مستقل تعریف شده باشد (بدون شناسه نقش)، مجموع نیازمندی‌های نقش نباید از آن تجاوز کند.
            // اما اگر دپارتمان مسئولیت‌محور است یا تخصص مستقل ندارد، ظرفیت از خود نقش‌ها تأمین می‌شود.
            var pureSpecialtyCapacity = (shift.RequiredSpecialties ?? Enumerable.Empty<ShiftRequiredSpecialty>())
                .Where(s => !s.DepartmentResponsibilityId.HasValue)
                .Sum(s => s.RequiredTottalCount ?? 0);

            if (pureSpecialtyCapacity > 0)
            {
                var existingOtherResponsibilities = (shift.RequiredResponsibilities ?? Enumerable.Empty<ShiftRequiredResponsibility>())
                    .Where(r => !currentId.HasValue || r.Id != currentId.Value)
                    .ToList();

                var sumResponsibilities = existingOtherResponsibilities.Sum(r => r.RequiredTotalCount ?? 0) + reqTotal;
                if (sumResponsibilities > pureSpecialtyCapacity)
                {
                    return (false, $"مجموع نفرات مسئولیت‌ها ({sumResponsibilities} نفر) نمی‌تواند از ظرفیت تخصص‌های مستقل شیفت ({pureSpecialtyCapacity} نفر) بیشتر باشد.");
                }
            }

            return (true, null);
        }
    }
}
