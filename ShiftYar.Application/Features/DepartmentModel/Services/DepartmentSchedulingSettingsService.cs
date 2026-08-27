using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using ShiftYar.Application.Features.ShiftModel.Filters;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Interfaces.DepartmentModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
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
        private readonly IEfRepository<Department> _departmentRepository;
        private readonly IEfRepository<User> _userRepository;
        private readonly IEfRepository<Shift> _shiftRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<DepartmentSchedulingSettingsService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DepartmentSchedulingSettingsService(
            IEfRepository<DepartmentSchedulingSettings> repository,
            IEfRepository<Department> departmentRepository,
            IEfRepository<User> userRepository,
            IEfRepository<Shift> shiftRepository,
            IMapper mapper,
            ILogger<DepartmentSchedulingSettingsService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _departmentRepository = departmentRepository;
            _userRepository = userRepository;
            _shiftRepository = shiftRepository;
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
            NormalizeMaxShiftsPerDay(dto);

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

            NormalizeMaxShiftsPerDay(dto);

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

            if (!MaxShiftsPerDayRules.IsValidSetting(dto.MaxShiftsPerDay))
            {
                message = MaxShiftsPerDayRules.InvalidSettingMessage;
                return false;
            }

            if (dto.EnforceMaxShiftsPerDay == true && !MaxShiftsPerDayRules.IsValidSetting(dto.MaxShiftsPerDay ?? 2))
            {
                message = MaxShiftsPerDayRules.InvalidSettingMessage;
                return false;
            }

            // اعتبارسنجی تنظیمات شب‌دوست/شب‌گریز
            if (dto.NightShiftPreferenceType.HasValue && (dto.NightShiftPreferenceType < 0 || dto.NightShiftPreferenceType > 2))
            {
                message = "نوع تنظیمات شب باید بین 0 تا 2 باشد (0=شب‌دوست، 1=شب‌گریز، 2=خنثی).";
                return false;
            }
            if (!ok(dto.NightShiftPreferenceWeight)) { message = "وزن تنظیمات شب‌دوست/شب‌گریز باید نامنفی باشد."; return false; }

            // اعتبارسنجی وزن الزام مسئول شیفت
            if (!ok(dto.ShiftManagerRequirementWeight)) { message = "وزن الزام حضور مسئول در شیفت‌ها باید نامنفی باشد."; return false; }

            // اعتبارسنجی تنظیمات توزیع بر اساس سابقه (صبح / عصر / شب)
            if (dto.MorningShiftDistributionType.HasValue && (dto.MorningShiftDistributionType < 0 || dto.MorningShiftDistributionType > 2))
            {
                message = "نوع توزیع شیفت صبح باید بین 0 تا 2 باشد (0=سابقه بیشتر، 1=سابقه کمتر، 2=خنثی).";
                return false;
            }
            if (!ok(dto.MorningShiftDistributionWeight)) { message = "وزن توزیع شیفت صبح بر اساس سابقه باید نامنفی باشد."; return false; }

            if (dto.EveningShiftDistributionType.HasValue && (dto.EveningShiftDistributionType < 0 || dto.EveningShiftDistributionType > 2))
            {
                message = "نوع توزیع شیفت عصر باید بین 0 تا 2 باشد (0=سابقه بیشتر، 1=سابقه کمتر، 2=خنثی).";
                return false;
            }
            if (!ok(dto.EveningShiftDistributionWeight)) { message = "وزن توزیع شیفت عصر بر اساس سابقه باید نامنفی باشد."; return false; }

            if (dto.NightShiftDistributionType.HasValue && (dto.NightShiftDistributionType < 0 || dto.NightShiftDistributionType > 2))
            {
                message = "نوع توزیع شیفت شب باید بین 0 تا 2 باشد (0=سابقه بیشتر، 1=سابقه کمتر، 2=خنثی).";
                return false;
            }
            if (!ok(dto.NightShiftDistributionWeight)) { message = "وزن توزیع شیفت‌های شب بر اساس سابقه باید نامنفی باشد."; return false; }
            if (!ok(dto.SeniorityDistributionSlope)) { message = "شیب توزیع بر اساس سابقه باید نامنفی باشد."; return false; }

            return true;
        }

        /// <summary>
        /// اعمال تنظیمات پیش‌فرض برای توزیع بر اساس سابقه (صبح/عصر/شب)
        /// </summary>
        private void ApplyDefaultNightShiftDistributionSettings(DepartmentSchedulingSettingsDtoAdd dto)
        {
            if (!dto.EnableMorningShiftDistributionBySeniority.HasValue)
                dto.EnableMorningShiftDistributionBySeniority = false;
            if (!dto.MorningShiftDistributionType.HasValue)
                dto.MorningShiftDistributionType = 2;
            if (!dto.MorningShiftDistributionWeight.HasValue)
                dto.MorningShiftDistributionWeight = 0.0;

            if (!dto.EnableEveningShiftDistributionBySeniority.HasValue)
                dto.EnableEveningShiftDistributionBySeniority = false;
            if (!dto.EveningShiftDistributionType.HasValue)
                dto.EveningShiftDistributionType = 2;
            if (!dto.EveningShiftDistributionWeight.HasValue)
                dto.EveningShiftDistributionWeight = 0.0;

            if (!dto.EnableNightShiftDistributionBySeniority.HasValue)
                dto.EnableNightShiftDistributionBySeniority = false;
            if (!dto.NightShiftDistributionType.HasValue)
                dto.NightShiftDistributionType = 2;
            if (!dto.NightShiftDistributionWeight.HasValue)
                dto.NightShiftDistributionWeight = 0.0;
            if (!dto.SeniorityDistributionSlope.HasValue)
                dto.SeniorityDistributionSlope = 1.0;
        }

        private static void NormalizeMaxShiftsPerDay(DepartmentSchedulingSettingsDtoAdd dto)
        {
            if (dto.MaxShiftsPerDay.HasValue)
            {
                dto.MaxShiftsPerDay = Math.Clamp(dto.MaxShiftsPerDay.Value, MaxShiftsPerDayRules.MinAllowed, MaxShiftsPerDayRules.MaxAllowed);
            }
        }

        public async Task<ApiResponse<string>> DeleteSettingAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return ApiResponse<string>.Fail("تنظیمات یافت نشد.");
            _repository.Delete(entity);
            await _repository.SaveAsync();
            return ApiResponse<string>.Success("تنظیمات با موفقیت حذف شد.");
        }

        public async Task<ApiResponse<DepartmentSchedulingSettingsDtoGet>> ApplyDefaultSettingsAsync(int departmentId)
        {
            if (departmentId <= 0)
            {
                return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail("شناسه دپارتمان نامعتبر است.");
            }

            var department = await _departmentRepository.GetByIdAsync(departmentId);
            if (department == null)
            {
                return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail("دپارتمان یافت نشد.");
            }

            var usersResult = await _userRepository.GetByFilterAsync(
                new UserFilter
                {
                    DepartmentId = departmentId,
                    IsActive = true,
                    PageNumber = 1,
                    PageSize = 5000
                },
                Array.Empty<string>());

            var shiftsResult = await _shiftRepository.GetByFilterAsync(
                new ShiftFilter
                {
                    DepartmentId = departmentId,
                    PageNumber = 1,
                    PageSize = 500
                },
                new[] { "RequiredSpecialties", "RequiredSpecialties.Specialty" });

            var profile = DepartmentSchedulingProfileFactory.Create(
                department,
                usersResult.Items.ToList(),
                shiftsResult.Items.ToList());

            var dto = DepartmentSchedulingDefaultSettingsBuilder.Build(profile);
            NormalizeMaxShiftsPerDay(dto);

            if (!ValidateWeights(dto, out var validationMessage))
            {
                return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Fail(validationMessage);
            }

            var existing = await _repository.GetByFilterAsync(
                new DepartmentSchedulingSettingsFilter
                {
                    DepartmentId = departmentId,
                    PageNumber = 1,
                    PageSize = 1
                },
                Array.Empty<string>());

            var entity = existing.Items.FirstOrDefault();
            var userId = Convert.ToInt16(_httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (entity == null)
            {
                entity = _mapper.Map<DepartmentSchedulingSettings>(dto);
                entity.CreateDate = DateTime.Now;
                entity.TheUserId = userId;
                await _repository.AddAsync(entity);
                await _repository.SaveAsync();

                _logger.LogInformation(
                    "Applied default department scheduling settings (create) for DepartmentId={DepartmentId}, ActiveUsers={ActiveUsers}, NightHeadcount={NightHeadcount}",
                    departmentId,
                    profile.ActiveUserCount,
                    profile.NightHeadcountPerShift);

                var created = _mapper.Map<DepartmentSchedulingSettingsDtoGet>(entity);
                return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Success(
                    created,
                    "تنظیمات پیش‌فرض بهینه با موفقیت ایجاد شد.");
            }

            _mapper.Map(dto, entity);
            entity.UpdateDate = DateTime.Now;
            entity.TheUserId = userId;
            _repository.Update(entity);
            await _repository.SaveAsync();

            _logger.LogInformation(
                "Applied default department scheduling settings (update) for DepartmentId={DepartmentId}, ActiveUsers={ActiveUsers}, NightHeadcount={NightHeadcount}",
                departmentId,
                profile.ActiveUserCount,
                profile.NightHeadcountPerShift);

            var updated = _mapper.Map<DepartmentSchedulingSettingsDtoGet>(entity);
            return ApiResponse<DepartmentSchedulingSettingsDtoGet>.Success(
                updated,
                "تنظیمات پیش‌فرض بهینه با موفقیت اعمال شد.");
        }
    }
}
