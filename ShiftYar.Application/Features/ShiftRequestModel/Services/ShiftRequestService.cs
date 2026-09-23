using AutoMapper;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using ShiftYar.Application.Features.ShiftModel.Filters;
using ShiftYar.Application.Features.ShiftModel.Services;
using ShiftYar.Application.Features.ShiftRequestModel.Filters;
using ShiftYar.Application.Features.UserModel.Services;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ShiftRequestModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using ShiftYar.Domain.Enums.ShiftModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftRequestModel.Services
{
    public class ShiftRequestService : IShiftRequestService
    {
        private readonly IEfRepository<ShiftRequest> _repository;
        private readonly IEfRepository<User> _repositoryUser;
        private readonly IEfRepository<Department> _repositorDepartment;
        private readonly IEfRepository<DepartmentSchedulingSettings> _deptSettingsRepository;
        private readonly IEfRepository<Shift> _shiftRepository;
        private readonly IEfRepository<UserMonthlyNightQuota> _monthlyNightQuotaRepository;
        private readonly IEfRepository<ShiftDate> _shiftDateRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<ShiftRequestService> _logger;

        public ShiftRequestService(
            IEfRepository<ShiftRequest> repository,
            IEfRepository<User> repositoryUser,
            IEfRepository<Department> repositorDepartment,
            IEfRepository<DepartmentSchedulingSettings> deptSettingsRepository,
            IEfRepository<Shift> shiftRepository,
            IEfRepository<UserMonthlyNightQuota> monthlyNightQuotaRepository,
            IEfRepository<ShiftDate> shiftDateRepository,
            IMapper mapper,
            ILogger<ShiftRequestService> logger)
        {
            _repository = repository;
            _repositoryUser = repositoryUser;
            _repositorDepartment = repositorDepartment;
            _deptSettingsRepository = deptSettingsRepository;
            _shiftRepository = shiftRepository;
            _monthlyNightQuotaRepository = monthlyNightQuotaRepository;
            _shiftDateRepository = shiftDateRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<ShiftRequestDtoGet>> CreateShiftRequestAsync(ShiftRequestDtoAdd dto)
        {
            try
            {
                //استخراج دپارتمان کاربر، برای دسترسی به سوپروایزر دپارتمان
                var userDepatment = await _repositoryUser.GetByIdAsync(dto.UserId);
                var userDepatmentId = userDepatment?.DepartmentId;

                if(!userDepatmentId.HasValue)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("کاربر درخواست دهنده، متعلق به هیچ دپارتمانی نیست.");

                //استخراج شناسه سوپروایزر
                var department = await _repositorDepartment.GetByIdAsync(userDepatmentId.Value);
                var supervisorId = department?.SupervisorId;

                if(!supervisorId.HasValue)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("fبرای دپارتمان کاربر درخواست دهنده، سوپروایزر تعیین نشده است.");

                var entity = _mapper.Map<ShiftRequest>(dto);
                entity.RequestDate = DateConverter.ConvertToGregorianDate(dto.RequestPersianDate);
                entity.SupervisorId = supervisorId;
                entity.Status = RequestStatus.Pending;

                var isFixed = userDepatment?.ShiftType == ShiftEnums.ShiftTypes.FixedShift;
                var typeError = ValidateOnShiftRequestType(entity.RequestAction, entity.RequestType, entity.ShiftLabel, isFixed);
                if (typeError != null)
                {
                    return ApiResponse<ShiftRequestDtoGet>.Fail(typeError);
                }

                if (isFixed && !entity.ShiftLabel.HasValue && entity.RequestAction == RequestAction.RequestToBeOnShift)
                {
                    entity.ShiftLabel = userDepatment?.ShiftSubType == ShiftEnums.ShiftSubTypes.FixedEvening
                        ? ShiftEnums.ShiftLabel.Evening
                        : ShiftEnums.ShiftLabel.Morning;
                }

                // فقط مقادیر خارج از enum (مثل Shift.Id=3) را به‌عنوان ShiftId remap کن.
                // Id=1/2 با Evening/Night هم‌عددند؛ تفسیر آن‌ها به‌عنوان Id درخواست صحیح را خراب می‌کند.
                if (entity.ShiftLabel.HasValue && entity.RequestType == RequestType.SpecificShift)
                {
                    var raw = (int)entity.ShiftLabel.Value;
                    if (!Enum.IsDefined(typeof(ShiftEnums.ShiftLabel), raw))
                    {
                        var (shifts, _) = await _shiftRepository.GetByFilterAsync(
                            new ShiftFilter
                            {
                                DepartmentId = userDepatmentId.Value,
                                PageNumber = 1,
                                PageSize = 100
                            });

                        var (resolved, _) = ShiftLabelResolver.Resolve(raw, shifts.ToList());
                        if (!Enum.IsDefined(typeof(ShiftEnums.ShiftLabel), (int)resolved))
                        {
                            return ApiResponse<ShiftRequestDtoGet>.Fail(
                                $"مقدار ShiftLabel نامعتبر است ({raw}). باید Morning=0، Evening=1 یا Night=2 باشد.");
                        }

                        _logger.LogWarning(
                            "CreateShiftRequest: remapping ShiftLabel raw={Raw} → {Label} (interpreted as ShiftId) for UserId={UserId}",
                            raw, resolved, dto.UserId);
                        entity.ShiftLabel = resolved;
                    }
                }

                var nightQuotaError = await ValidateNightOnRequestAgainstMonthlyQuotaAsync(entity);
                if (nightQuotaError != null)
                {
                    return ApiResponse<ShiftRequestDtoGet>.Fail(nightQuotaError);
                }

                await _repository.AddAsync(entity);
                await _repository.SaveAsync();
                var result = _mapper.Map<ShiftRequestDtoGet>(entity);
                return ApiResponse<ShiftRequestDtoGet>.Success(result, "درخواست با موفقیت ثبت شد.");
            }
            catch (Exception ex)
            {
                return ApiResponse<ShiftRequestDtoGet>.Fail($"خطا در ثبت درخواست: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ShiftRequestDtoGet>> CreateShiftRequestForLeaveAsync(ShiftRequestForLeaveDtoAdd dto)
        {
            try
            {
                //استخراج دپارتمان کاربر، برای دسترسی به سوپروایزر دپارتمان
                var userDepatment = await _repositoryUser.GetByIdAsync(dto.UserId);
                var userDepatmentId = userDepatment?.DepartmentId;

                if (!userDepatmentId.HasValue)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("کاربر درخواست دهنده، متعلق به هیچ دپارتمانی نیست.");

                //استخراج شناسه سوپروایزر
                var department = await _repositorDepartment.GetByIdAsync(userDepatmentId.Value);
                var supervisorId = department?.SupervisorId;

                if (!supervisorId.HasValue)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("برای دپارتمان کاربر درخواست دهنده، سوپروایزر تعیین نشده است.");

                var startDate = DateConverter.ConvertToGregorianDate(dto.StartPersianDate);
                var endDate = DateConverter.ConvertToGregorianDate(dto.EndPersianDate);

                if (endDate < startDate)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");

                int createdCount = 0;
                ShiftRequest? lastCreated = null;

                for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    // جلوگیری از ثبت درخواست تکراری در همان تاریخ برای همان کاربر
                    bool exists = await _repository.ExistsAsync(x =>
                        x.UserId == dto.UserId &&
                        x.RequestDate == date &&
                        x.RequestType == RequestType.FullDay &&
                        x.RequestAction == RequestAction.RequestToBeOffShift);

                    if (exists)
                        continue;

                    var entity = new ShiftRequest
                    {
                        UserId = dto.UserId,
                        RequestDate = date,
                        RequestType = RequestType.FullDay,
                        RequestAction = RequestAction.RequestToBeOffShift,
                        Status = RequestStatus.Pending,
                        Reason = dto.Reason,
                        SupervisorId = supervisorId
                    };

                    await _repository.AddAsync(entity);
                    createdCount++;
                    lastCreated = entity;
                }

                if (createdCount > 0)
                    await _repository.SaveAsync();

                var result = lastCreated != null ? _mapper.Map<ShiftRequestDtoGet>(lastCreated) : null;
                var message = createdCount > 0
                    ? $"درخواست مرخصی برای {createdCount} روز با موفقیت ثبت شد."
                    : "در این بازه زمانی درخواست جدیدی ثبت نشد.";
                return ApiResponse<ShiftRequestDtoGet>.Success(result, message);
            }
            catch (Exception ex)
            {
                return ApiResponse<ShiftRequestDtoGet>.Fail($"خطا در ثبت درخواست: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ShiftRequestDtoGet>> UpdateShiftRequestByUserAsync(int id, ShiftRequestDtoAdd dto)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id, "User", "Supervisor");
                if (entity == null)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("درخواست مورد نظر یافت نشد.");
                if (entity.Status != RequestStatus.Pending)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("امکان ویرایش این درخواست وجود ندارد.");

                entity.RequestDate = DateConverter.ConvertToGregorianDate(dto.RequestPersianDate);

                _mapper.Map(dto, entity);

                var isFixed = entity.User?.ShiftType == ShiftEnums.ShiftTypes.FixedShift;
                var typeError = ValidateOnShiftRequestType(entity.RequestAction, entity.RequestType, entity.ShiftLabel, isFixed);
                if (typeError != null)
                {
                    return ApiResponse<ShiftRequestDtoGet>.Fail(typeError);
                }

                if (isFixed && !entity.ShiftLabel.HasValue && entity.RequestAction == RequestAction.RequestToBeOnShift)
                {
                    entity.ShiftLabel = entity.User?.ShiftSubType == ShiftEnums.ShiftSubTypes.FixedEvening
                        ? ShiftEnums.ShiftLabel.Evening
                        : ShiftEnums.ShiftLabel.Morning;
                }

                var nightQuotaError = await ValidateNightOnRequestAgainstMonthlyQuotaAsync(entity);
                if (nightQuotaError != null)
                {
                    return ApiResponse<ShiftRequestDtoGet>.Fail(nightQuotaError);
                }

                await _repository.SaveAsync();
                var result = _mapper.Map<ShiftRequestDtoGet>(entity);
                return ApiResponse<ShiftRequestDtoGet>.Success(result, "درخواست با موفقیت ویرایش شد.");
            }
            catch (Exception ex)
            {
                return ApiResponse<ShiftRequestDtoGet>.Fail($"خطا در ویرایش درخواست: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ShiftRequestDtoGet>> UpdateShiftRequestBySupervisorAsync(int id, ShiftRequestDtoUpdateBySupervisor dto)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id, "User", "Supervisor", "User.Specialty");
                if (entity == null)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("درخواست مورد نظر یافت نشد.");

                if (dto.Status != RequestStatus.Approved && dto.Status != RequestStatus.Rejected)
                {
                    return ApiResponse<ShiftRequestDtoGet>.Fail(
                        "وضعیت ارسالی نامعتبر است. فقط تأیید (Approved) یا رد (Rejected) مجاز است.");
                }

                if (entity.Status == RequestStatus.Rejected)
                {
                    return ApiResponse<ShiftRequestDtoGet>.Fail(
                        "این درخواست قبلاً رد شده است. در صورت نیاز آن را حذف و درخواست جدید ثبت کنید.");
                }

                if (entity.Status == RequestStatus.Approved)
                {
                    // لغو تأیید اشتباه: فقط تبدیل به رد مجاز است
                    if (dto.Status != RequestStatus.Rejected)
                    {
                        return ApiResponse<ShiftRequestDtoGet>.Fail(
                            "برای درخواست تأییدشده فقط امکان رد (لغو تأیید) وجود دارد.");
                    }
                }
                else if (entity.Status == RequestStatus.Pending)
                {
                    if (dto.Status == RequestStatus.Approved)
                    {
                        var validationError = await ValidateApprovalAsync(entity);
                        if (validationError != null)
                        {
                            return ApiResponse<ShiftRequestDtoGet>.Fail(validationError);
                        }
                    }
                }
                else
                {
                    return ApiResponse<ShiftRequestDtoGet>.Fail("وضعیت فعلی درخواست قابل بررسی نیست.");
                }

                entity.Status = dto.Status;
                entity.SupervisorComment = dto.SupervisorComment;
                entity.ApprovalDate = DateTime.Now;

                await _repository.SaveAsync();
                var result = _mapper.Map<ShiftRequestDtoGet>(entity);
                return ApiResponse<ShiftRequestDtoGet>.Success(
                    result,
                    dto.Status == RequestStatus.Rejected
                        ? "درخواست با موفقیت رد شد."
                        : "درخواست با موفقیت تأیید شد.");
            }
            catch (Exception ex)
            {
                return ApiResponse<ShiftRequestDtoGet>.Fail($"خطا در بررسی درخواست: {ex.Message}");
            }
        }

        public async Task<ApiResponse<string>> DeleteShiftRequestAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<string>.Fail("درخواست مورد نظر یافت نشد.");
                if (entity.Status != RequestStatus.Pending && entity.Status != RequestStatus.Rejected)
                {
                    return ApiResponse<string>.Fail(
                        "فقط درخواست‌های در انتظار بررسی یا ردشده قابل حذف هستند. برای حذف درخواست تأییدشده، ابتدا آن را رد کنید.");
                }

                _repository.Delete(entity);
                await _repository.SaveAsync();
                return ApiResponse<string>.Success("درخواست با موفقیت حذف شد.");
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.Fail($"خطا در حذف درخواست: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ShiftRequestDtoGet>> GetShiftRequestAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id, "User", "Supervisor");
                if (entity == null)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("درخواست مورد نظر یافت نشد.");
                var result = _mapper.Map<ShiftRequestDtoGet>(entity);
                return ApiResponse<ShiftRequestDtoGet>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<ShiftRequestDtoGet>.Fail($"خطا در دریافت درخواست: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ShiftRequestDtoGet>>> GetUserShiftRequestsAsync(int userId)
        {
            try
            {
                ShiftRequestFilter filter = new ShiftRequestFilter { UserId = userId };
                (List<ShiftRequest> items, int _) = await _repository.GetByFilterAsync(filter, "User", "Supervisor");
                var result = items.Select(x => _mapper.Map<ShiftRequestDtoGet>(x)).ToList();
                return ApiResponse<List<ShiftRequestDtoGet>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ShiftRequestDtoGet>>.Fail($"خطا در دریافت لیست درخواست‌ها: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ShiftRequestDtoGet>>> GetAllShiftRequestsAsync()
        {
            try
            {
                ShiftRequestFilter filter = new ShiftRequestFilter();
                (List<ShiftRequest> items, int _) = await _repository.GetByFilterAsync(filter, "User", "Supervisor");
                var result = items.Select(x => _mapper.Map<ShiftRequestDtoGet>(x)).ToList();
                return ApiResponse<List<ShiftRequestDtoGet>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ShiftRequestDtoGet>>.Fail($"خطا در دریافت لیست درخواست‌ها: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PagedResponse<ShiftRequestDtoGet>>> GetShiftRequestsAsync(ShiftRequestFilter filter)
        {
            try
            {
                _logger.LogInformation("Getting filtered shiftRequests");

                var (items, totalCount) = await _repository.GetByFilterAsync(filter,
                    "User",
                    "Supervisor");

                var shiftRequests = items.Select(s => _mapper.Map<ShiftRequestDtoGet>(s)).ToList();

                var pagedResponse = new PagedResponse<ShiftRequestDtoGet>
                {
                    Items = shiftRequests,
                    TotalCount = totalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
                };

                return ApiResponse<PagedResponse<ShiftRequestDtoGet>>.Success(pagedResponse, "لیست درخواست های شیفت‌ با موفقیت دریافت شد.");
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت درخواست های شیفت با خطا مواجه شد : " + ex.Message);
            }
        }

        private static string? ValidateOnShiftRequestType(
            RequestAction? action,
            RequestType? type,
            ShiftEnums.ShiftLabel? shiftLabel,
            bool isFixedShiftUser = false)
        {
            if (action != RequestAction.RequestToBeOnShift)
            {
                return null;
            }

            // برای پرسنل فیکس، حضور کل‌روز معادل شیفت فیکس آنها است و مجاز است
            if (type == RequestType.FullDay)
            {
                if (isFixedShiftUser)
                {
                    return null;
                }

                return
                    "درخواست حضور کل‌روز مجاز نیست. ترکیب‌های مجاز در یک روز: صبح+عصر یا صبح+شب؛ " +
                    "برای حضور باید نوع درخواست «شیفت مشخص» باشد و یکی از شیفت‌های صبح، عصر یا شب انتخاب شود.";
            }

            if (type == RequestType.SpecificShift && !shiftLabel.HasValue && !isFixedShiftUser)
            {
                return "برای درخواست حضور در شیفت مشخص، انتخاب شیفت (صبح/عصر/شب) الزامی است.";
            }

            return null;
        }

        private async Task<string?> ValidateApprovalAsync(ShiftRequest entity)
        {
            var isFixed = entity.User?.ShiftType == ShiftEnums.ShiftTypes.FixedShift;
            var typeError = ValidateOnShiftRequestType(entity.RequestAction, entity.RequestType, entity.ShiftLabel, isFixed);
            if (typeError != null)
            {
                return typeError;
            }

            var capacityError = await ValidateOnShiftCapacityAsync(entity);
            if (capacityError != null)
            {
                return capacityError;
            }

            var nightQuotaError = await ValidateNightOnRequestAgainstMonthlyQuotaAsync(entity);
            if (nightQuotaError != null)
            {
                return nightQuotaError;
            }


            var leaveCapacityError = await ValidateDailyLeaveCapacityAsync(entity);
            if (leaveCapacityError != null)
            {
                return leaveCapacityError;
            }

            if (entity.RequestAction != RequestAction.RequestToBeOnShift ||
                entity.RequestType != RequestType.SpecificShift ||
                !entity.ShiftLabel.HasValue ||
                entity.User == null)
            {
                return null;
            }

            if (ShiftManagerRules.NormalizeLevel(entity.User.ShiftManagerLevel).HasValue
                || entity.User.CanBeShiftManager == true)
            {
                return null;
            }

            if (!entity.User.DepartmentId.HasValue)
            {
                return null;
            }

            var (shifts, _) = await _shiftRepository.GetByFilterAsync(
                new ShiftFilter
                {
                    DepartmentId = entity.User.DepartmentId.Value,
                    PageNumber = 1,
                    PageSize = 100
                },
                "RequiredSpecialties");

            var matchingShifts = shifts.Where(s => s.Label == entity.ShiftLabel).ToList();
            var anyShiftRequiresManager = matchingShifts.Any(s => s.ManagerRequiredCount > 0);

            if (!anyShiftRequiresManager || !entity.User.SpecialtyId.HasValue)
            {
                return null;
            }

            foreach (var shift in matchingShifts)
            {
                var specialtyRequirement = shift.RequiredSpecialties?
                    .FirstOrDefault(rs => rs.SpecialtyId == entity.User.SpecialtyId);

                if (specialtyRequirement != null && (specialtyRequirement.RequiredTottalCount ?? 0) == 1)
                {
                    return "این شیفت تک‌نفره است و طبق تعریف شیفت باید توسط فردی با صلاحیت مدیریت شیفت پوشش داده شود. لطفاً درخواست را رد کنید یا ابتدا صلاحیت «مدیر شیفت» را برای این کاربر فعال کنید.";
                }
            }

            return null;
        }

        /// <summary>
        /// سوپروایزر نباید درخواست حضور (ON) بیش از ظرفیت شیفت/تخصص در همان روز تأیید کند.
        /// </summary>
        private async Task<string?> ValidateOnShiftCapacityAsync(ShiftRequest entity)
        {
            if (entity.RequestAction != RequestAction.RequestToBeOnShift ||
                entity.RequestType != RequestType.SpecificShift ||
                !entity.ShiftLabel.HasValue ||
                !entity.RequestDate.HasValue ||
                entity.User == null ||
                !entity.User.SpecialtyId.HasValue ||
                !entity.User.DepartmentId.HasValue)
            {
                return null;
            }

            var departmentId = entity.User.DepartmentId.Value;
            var specialtyId = entity.User.SpecialtyId.Value;
            var requestDate = entity.RequestDate.Value.Date;
            var shiftLabel = entity.ShiftLabel.Value;

            var (shifts, _) = await _shiftRepository.GetByFilterAsync(
                new ShiftFilter
                {
                    DepartmentId = departmentId,
                    PageNumber = 1,
                    PageSize = 100
                },
                "RequiredSpecialties");

            var capacity = ApprovedOnShiftCapacityValidator.ResolveCapacityForSpecialty(
                shifts, shiftLabel, specialtyId, await IsHolidayDateAsync(requestDate));
            if (capacity <= 0)
            {
                return null;
            }

            var (departmentUsers, _) = await _repositoryUser.GetByFilterAsync(
                new SimpleFilter<User>(u =>
                    u.DepartmentId == departmentId &&
                    u.SpecialtyId == specialtyId));
            var departmentUserIds = departmentUsers
                .Where(u => u.Id.HasValue)
                .Select(u => u.Id!.Value)
                .ToHashSet();
            if (departmentUserIds.Count == 0)
            {
                return null;
            }

            var (approvedRequests, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<ShiftRequest>(r =>
                    r.Id != entity.Id &&
                    r.Status == RequestStatus.Approved &&
                    r.RequestAction == RequestAction.RequestToBeOnShift &&
                    r.RequestType == RequestType.SpecificShift &&
                    r.ShiftLabel == shiftLabel &&
                    r.RequestDate != null &&
                    r.RequestDate.Value.Date == requestDate &&
                    r.UserId != null &&
                    departmentUserIds.Contains(r.UserId.Value)),
                "User");

            var approvedNames = approvedRequests
                .Select(r => FormatUserDisplayName(r.User))
                .ToList();

            return ApprovedOnShiftCapacityValidator.BuildExceededCapacityMessage(
                approvedRequests.Count,
                capacity,
                shiftLabel,
                requestDate,
                approvedNames);
        }

        /// <summary>
        /// سوپروایزر نباید درخواست مرخصی بیش از سقف مجاز روزانه را تأیید کند.
        /// سقف مجاز = کل پرسنل فعال - (مجموع شیفت‌های امروز + شیفت شب روز قبل)
        /// </summary>
        private async Task<string?> ValidateDailyLeaveCapacityAsync(ShiftRequest entity)
        {
            if (entity.RequestAction != RequestAction.RequestToBeOffShift ||
                entity.RequestType != RequestType.FullDay ||
                !entity.RequestDate.HasValue ||
                entity.User == null ||
                !entity.User.DepartmentId.HasValue ||
                !entity.User.SpecialtyId.HasValue)
            {
                return null;
            }

            var departmentId = entity.User.DepartmentId.Value;
            var specialtyId = entity.User.SpecialtyId.Value;
            var requestDate = entity.RequestDate.Value.Date;
            var prevDate = requestDate.AddDays(-1);

            var (departmentUsers, _) = await _repositoryUser.GetByFilterAsync(
                new SimpleFilter<User>(u =>
                    u.DepartmentId == departmentId &&
                    u.SpecialtyId == specialtyId &&
                    (u.IsActive == null || u.IsActive == true)));

            var departmentUserIds = departmentUsers
                .Where(u => u.Id.HasValue)
                .Select(u => u.Id!.Value)
                .ToHashSet();

            if (departmentUserIds.Count == 0)
            {
                return null;
            }

            var (shifts, _) = await _shiftRepository.GetByFilterAsync(
                new ShiftFilter
                {
                    DepartmentId = departmentId,
                    PageNumber = 1,
                    PageSize = 100
                },
                "RequiredSpecialties");

            var isHolidayToday = await IsHolidayDateAsync(requestDate);
            var isHolidayYesterday = await IsHolidayDateAsync(prevDate);

            var todayShiftDemand = ApprovedLeaveCapacityValidator.CalculateDailyShiftDemandForSpecialty(
                shifts, specialtyId, isHolidayToday);
            var yesterdayNightDemand = ApprovedLeaveCapacityValidator.CalculateNightShiftDemandForSpecialty(
                shifts, specialtyId, isHolidayYesterday);

            var totalActivePersonnel = departmentUsers.Count;
            var maxCapacity = ApprovedLeaveCapacityValidator.CalculateMaxDailyLeaveCapacity(
                totalActivePersonnel, todayShiftDemand, yesterdayNightDemand);

            var (approvedRequests, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<ShiftRequest>(r =>
                    r.Id != entity.Id &&
                    r.Status == RequestStatus.Approved &&
                    r.RequestAction == RequestAction.RequestToBeOffShift &&
                    r.RequestType == RequestType.FullDay &&
                    r.RequestDate != null &&
                    r.RequestDate.Value.Date == requestDate &&
                    r.UserId != null &&
                    departmentUserIds.Contains(r.UserId.Value)),
                "User");

            var approvedUserIds = approvedRequests
                .Where(r => r.UserId.HasValue)
                .Select(r => r.UserId!.Value)
                .Distinct()
                .ToHashSet();

            var approvedNames = approvedRequests
                .Where(r => r.UserId.HasValue && approvedUserIds.Contains(r.UserId.Value))
                .GroupBy(r => r.UserId)
                .Select(g => FormatUserDisplayName(g.First().User))
                .ToList();

            var specialtyName = entity.User.Specialty?.SpecialtyName;

            return ApprovedLeaveCapacityValidator.BuildExceededCapacityMessage(
                approvedUserIds.Count,
                maxCapacity,
                totalActivePersonnel,
                todayShiftDemand,
                yesterdayNightDemand,
                requestDate,
                specialtyName,
                approvedNames);
        }

        private async Task<bool> IsHolidayDateAsync(DateTime requestDate)
        {
            var (shiftDates, _) = await _shiftDateRepository.GetByFilterAsync(
                new SimpleFilter<ShiftDate>(d => d.Date != null && d.Date.Value.Date == requestDate.Date));
            return shiftDates.Any(d => d.IsHoliday == true);
        }

        private static string FormatUserDisplayName(User? user)
        {
            if (user == null)
            {
                return "کاربر نامشخص";
            }

            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                return user.FullName.Trim();
            }

            return user.Id.HasValue ? $"کاربر {user.Id.Value}" : "کاربر نامشخص";
        }

        /// <summary>
        /// درخواست حضور در شیفت شب باید داخل سهمیه ماهانه کاربر بماند.
        /// </summary>
        private async Task<string?> ValidateNightOnRequestAgainstMonthlyQuotaAsync(ShiftRequest entity)
        {
            if (!NightQuotaRequestLinker.IsNightOnRequest(entity) ||
                !entity.UserId.HasValue ||
                !entity.RequestDate.HasValue)
            {
                return null;
            }

            var (year, month) = NightQuotaRequestLinker.GetPersianYearMonth(entity.RequestDate.Value);
            var (monthStart, monthEnd, _) = PersianMonthNightCalendar.GetMonthBounds(year, month);

            var (quotas, _) = await _monthlyNightQuotaRepository.GetByFilterAsync(
                new SimpleFilter<UserMonthlyNightQuota>(q =>
                    q.UserId == entity.UserId.Value &&
                    q.PersianYear == year &&
                    q.PersianMonth == month));
            var quota = quotas.FirstOrDefault();

            var (approvedRequests, _) = await _repository.GetByFilterAsync(
                new SimpleFilter<ShiftRequest>(r =>
                    r.UserId == entity.UserId.Value &&
                    r.Status == RequestStatus.Approved &&
                    r.RequestAction == RequestAction.RequestToBeOnShift &&
                    r.RequestType == RequestType.SpecificShift &&
                    r.ShiftLabel == ShiftEnums.ShiftLabel.Night &&
                    r.RequestDate != null &&
                    r.RequestDate >= monthStart &&
                    r.RequestDate <= monthEnd));

            // هنگام تأیید، خود این درخواست هنوز Approved نیست؛ هنگام ویرایش Pending هم شمرده نمی‌شود
            var approvedNightCount = NightQuotaRequestLinker.CountApprovedNightOnRequestsInMonth(
                approvedRequests, entity.UserId.Value, year, month, excludeRequestId: entity.Id);

            var nightError = NightQuotaRequestLinker.ValidateNightOnAgainstQuota(
                approvedNightCount, quota?.ExactNightShiftCount, year, month);
            if (nightError != null)
            {
                return nightError;
            }

            var (shiftDates, _) = await _shiftDateRepository.GetByFilterAsync(
                new SimpleFilter<ShiftDate>(d =>
                    d.Date != null &&
                    d.Date >= monthStart &&
                    d.Date <= monthEnd.AddDays(1)));
            var holidays = shiftDates
                .Where(d => d.IsHoliday == true && d.Date.HasValue)
                .Select(d => d.Date!.Value.Date)
                .ToHashSet();

            bool IsHolidayNight(DateTime date) =>
                HolidayWeekendNightRules.IsHolidayWeekendNight(date, holidays);

            var isHolidayNight = IsHolidayNight(entity.RequestDate.Value.Date);
            if (!isHolidayNight)
            {
                var approvedNonHolidayCount = NightQuotaRequestLinker.CountApprovedNonHolidayNightOnRequestsInMonth(
                    approvedRequests,
                    entity.UserId.Value,
                    year,
                    month,
                    IsHolidayNight,
                    excludeRequestId: entity.Id);

                return NightQuotaRequestLinker.ValidateNonHolidayOnLeavesRoomForHoliday(
                    approvedNonHolidayCount,
                    quota?.ExactNightShiftCount,
                    quota?.ExactHolidayWeekendNightShiftCount,
                    year,
                    month);
            }

            // شب تعطیل/آخرهفته: ExactHoliday فقط حداقل شیفت‌بندی است، نه سقف تأیید درخواست
            return null;
        }
    }
}
