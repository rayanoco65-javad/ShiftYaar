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
    public class DepartmentSchedulingSettingsService : IDepartmentSchedulingSettingsService
    {
        private readonly IEfRepository<DepartmentSchedulingSettings> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<DepartmentSchedulingSettingsService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DepartmentSchedulingSettingsService(IEfRepository<DepartmentSchedulingSettings> repository, IMapper mapper, ILogger<DepartmentSchedulingSettingsService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<DepartmentSchedulingSettingsDtoGet>>> GetSettingsAsync(DepartmentSchedulingSettingsFilter filter)
        {
            var result = await _repository.GetByFilterAsync(filter, "Department");
            var data = _mapper.Map<List<DepartmentSchedulingSettingsDtoGet>>(result.Items);

            var paged = new PagedResponse<DepartmentSchedulingSettingsDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };
            return ApiResponse<PagedResponse<DepartmentSchedulingSettingsDtoGet>>.Success(paged);
        }

        public async Task<ApiResponse<DepartmentSchedulingSettingsDtoGet>> GetSettingAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id, "Department");
            if (entity == null) return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail("تنظیمات یافت نشد.");
            var dto = _mapper.Map<DepartmentSchedulingSettingsDtoGet>(entity);
            return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Success(dto);
        }

        public async Task<ApiResponse<DepartmentSchedulingSettingsDtoGet>> CreateSettingAsync(DepartmentSchedulingSettingsDtoAdd dto)
        {
            // جلوگیری از ایجاد تنظیمات تکراری برای یک دپارتمان
            var exists = await _repository.ExistsAsync(s => s.DepartmentId == dto.DepartmentId);
            if (exists)
            {
                return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail("برای این دپارتمان قبلاً تنظیمات ثبت شده است. از مسیر ویرایش استفاده کنید.");
            }

            // اعتبارسنجی وزن‌ها
            if (!ValidateWeights(dto, out var validationMessage))
            {
                return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail(validationMessage);
            }

            // اعمال تنظیمات پیش‌فرض برای توزیع شیفت‌های شب
            ApplyDefaultNightShiftDistributionSettings(dto);

            var entity = _mapper.Map<DepartmentSchedulingSettings>(dto);
            entity.CreateDate = DateTime.Now;
            entity.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            await _repository.AddAsync(entity);
            await _repository.SaveAsync();
            var result = _mapper.Map<DepartmentSchedulingSettingsDtoGet>(entity);
            return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Success(result, "تنظیمات با موفقیت ایجاد شد.");
        }

        public async Task<ApiResponse<DepartmentSchedulingSettingsDtoGet>> UpdateSettingAsync(int id, DepartmentSchedulingSettingsDtoAdd dto)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail("تنظیمات یافت نشد.");

            // جلوگیری از وجود بیش از یک رکورد برای یک DepartmentId هنگام تغییر DepartmentId
            if (dto.DepartmentId != 0 && dto.DepartmentId != (entity.DepartmentId ?? 0))
            {
                var duplicate = await _repository.ExistsAsync(s => s.DepartmentId == dto.DepartmentId);
                if (duplicate)
                {
                    return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail("برای این دپارتمان تنظیمات دیگری وجود دارد.");
                }
            }

            // اعتبارسنجی وزن‌ها
            if (!ValidateWeights(dto, out var validationMessage))
            {
                return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail(validationMessage);
            }

            _mapper.Map(dto, entity);
            entity.CreateDate = DateTime.Now;
            entity.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));
            _repository.Update(entity);
            await _repository.SaveAsync();
            var result = _mapper.Map<DepartmentSchedulingSettingsDtoGet>(entity);
            return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Success(result, "تنظیمات با موفقیت ویرایش شد.");
        }

        private bool ValidateWeights(DepartmentSchedulingSettingsDtoAdd dto, out string message)
        {
            message = string.Empty;
            bool ok(double? v) => !v.HasValue || v.Value >= 0;
            bool okInt(int? v) => !v.HasValue || v.Value >= 0;

            // اعتبارسنجی وزن‌های اصلی
            if (!ok(dto.GenderBalanceWeight)) { message = "وزن تعادل جنسیتی باید نامنفی باشد."; return false; }
            if (!ok(dto.SpecialtyPreferenceWeight)) { message = "وزن ترجیح تخصص باید نامنفی باشد."; return false; }
            if (!ok(dto.UserUnwantedShiftWeight)) { message = "وزن شیفت ناخواسته باید نامنفی باشد."; return false; }
            if (!ok(dto.UserPreferredShiftWeight)) { message = "وزن شیفت ترجیحی باید نامنفی باشد."; return false; }
            if (!ok(dto.WeeklyMaxWeight)) { message = "وزن سقف هفتگی باید نامنفی باشد."; return false; }
            if (!ok(dto.MonthlyNightCapWeight)) { message = "وزن سقف شب ماهانه باید نامنفی باشد."; return false; }

            // اعتبارسنجی وزن‌های جدید
            if (!ok(dto.FairShiftCountBalanceWeight)) { message = "وزن تعادل تعداد شیفت باید نامنفی باشد."; return false; }
            if (!ok(dto.ExtraShiftRotationWeight)) { message = "وزن چرخش شیفت اضافه باید نامنفی باشد."; return false; }
            if (!ok(dto.ShiftLabelBalanceWeight)) { message = "وزن تعادل نوع شیفت باید نامنفی باشد."; return false; }

            // اعتبارسنجی تنظیمات حداقل شیفت برای پرسنل گردشی
            if (!okInt(dto.MinMorningShiftsForThreeShiftRotation)) { message = "حداقل شیفت صبح برای گردشی سه نوبت باید نامنفی باشد."; return false; }
            if (!okInt(dto.MinEveningShiftsForThreeShiftRotation)) { message = "حداقل شیفت عصر برای گردشی سه نوبت باید نامنفی باشد."; return false; }
            if (!okInt(dto.MinNightShiftsForThreeShiftRotation)) { message = "حداقل شیفت شب برای گردشی سه نوبت باید نامنفی باشد."; return false; }
            if (!okInt(dto.MinFirstShiftForTwoShiftRotation)) { message = "حداقل شیفت اول برای گردشی دو نوبت باید نامنفی باشد."; return false; }
            if (!okInt(dto.MinSecondShiftForTwoShiftRotation)) { message = "حداقل شیفت دوم برای گردشی دو نوبت باید نامنفی باشد."; return false; }

            // اعتبارسنجی تنظیمات شب‌دوست/شب‌گریز
            if (dto.NightShiftPreferenceType.HasValue && (dto.NightShiftPreferenceType < 0 || dto.NightShiftPreferenceType > 2))
            {
                message = "نوع تنظیمات شب باید بین 0 تا 2 باشد (0=شب‌دوست، 1=شب‌گریز، 2=خنثی).";
                return false;
            }
            if (!ok(dto.NightShiftPreferenceWeight)) { message = "وزن تنظیمات شب‌دوست/شب‌گریز باید نامنفی باشد."; return false; }

            // اعتبارسنجی وزن الزام مسئول شیفت
            if (!ok(dto.ShiftManagerRequirementWeight)) { message = "وزن الزام حضور مسئول در شیفت‌ها باید نامنفی باشد."; return false; }

            // اعتبارسنجی تنظیمات توزیع شیفت‌های شب بر اساس سابقه
            if (dto.NightShiftDistributionType.HasValue && (dto.NightShiftDistributionType < 0 || dto.NightShiftDistributionType > 2))
            {
                message = "نوع توزیع شیفت‌های شب باید بین 0 تا 2 باشد (0=شب‌دوست، 1=شب‌گریز، 2=خنثی).";
                return false;
            }
            if (!ok(dto.NightShiftDistributionWeight)) { message = "وزن توزیع شیفت‌های شب بر اساس سابقه باید نامنفی باشد."; return false; }
            if (!ok(dto.SeniorityDistributionSlope)) { message = "شیب توزیع بر اساس سابقه باید نامنفی باشد."; return false; }

            return true;
        }

        /// <summary>
        /// اعمال تنظیمات پیش‌فرض برای توزیع شیفت‌های شب بر اساس سابقه
        /// </summary>
        private void ApplyDefaultNightShiftDistributionSettings(DepartmentSchedulingSettingsDtoAdd dto)
        {
            if (!dto.EnableNightShiftDistributionBySeniority.HasValue)
                dto.EnableNightShiftDistributionBySeniority = true;

            if (!dto.NightShiftDistributionType.HasValue)
                dto.NightShiftDistributionType = 0; // پیش‌فرض: شب‌دوست

            if (!dto.NightShiftDistributionWeight.HasValue)
                dto.NightShiftDistributionWeight = 1.0;

            if (!dto.SeniorityDistributionSlope.HasValue)
                dto.SeniorityDistributionSlope = 1.0; // شیب پیش‌فرض
        }

        public async Task<ApiResponse<string>> DeleteSettingAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return ApiResponse<string>.Fail("تنظیمات یافت نشد.");
            _repository.Delete(entity);
            await _repository.SaveAsync();
            return ApiResponse<string>.Success("تنظیمات با موفقیت حذف شد.");
        }
    }
}
