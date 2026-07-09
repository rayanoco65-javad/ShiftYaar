using AutoMapper;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel;
using ShiftYar.Application.Features.ShiftModel.Filters;
using ShiftYar.Application.Features.ShiftRequestModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ShiftRequestModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
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
        private readonly IMapper _mapper;
        private readonly ILogger<ShiftRequestService> _logger;

        public ShiftRequestService(IEfRepository<ShiftRequest> repository, IEfRepository<User> repositoryUser, IEfRepository<Department> repositorDepartment, IMapper mapper, ILogger<ShiftRequestService> logger)
        {
            _repository = repository;
            _repositoryUser = repositoryUser;
            _repositorDepartment = repositorDepartment;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<ShiftRequestDtoGet>> CreateShiftRequestAsync(ShiftRequestDtoAdd dto)
        {
            try
            {
                //استخراج دپارتمان کاربر، برای دسترسی به سوپروایزر دپارتمان
                var userDepatmentId = _repositoryUser.GetByIdAsync(dto.UserId).Result.DepartmentId;

                if(!userDepatmentId.HasValue)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("کاربر درخواست دهنده، متعلق به هیچ دپارتمانی نیست.");

                //استخراج شناسه سوپروایزر
                var supervisorId = _repositorDepartment.GetByIdAsync(userDepatmentId).Result.SupervisorId;

                if(!supervisorId.HasValue)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("fبرای دپارتمان کاربر درخواست دهنده، سوپروایزر تعیین نشده است.");

                var entity = _mapper.Map<ShiftRequest>(dto);
                entity.RequestDate = DateConverter.ConvertToGregorianDate(dto.RequestPersianDate);
                entity.SupervisorId = supervisorId;
                entity.Status = RequestStatus.Pending;
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
                var userDepatmentId = _repositoryUser.GetByIdAsync(dto.UserId).Result.DepartmentId;

                if (!userDepatmentId.HasValue)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("کاربر درخواست دهنده، متعلق به هیچ دپارتمانی نیست.");

                //استخراج شناسه سوپروایزر
                var supervisorId = _repositorDepartment.GetByIdAsync(userDepatmentId).Result.SupervisorId;

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
                var entity = await _repository.GetByIdAsync(id, "User", "Supervisor");
                if (entity == null)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("درخواست مورد نظر یافت نشد.");
                if (entity.Status != RequestStatus.Pending)
                    return ApiResponse<ShiftRequestDtoGet>.Fail("درخواست قبلاً بررسی شده است.");

                entity.Status = dto.Status;
                entity.SupervisorComment = dto.SupervisorComment;
                entity.ApprovalDate = DateTime.Now;

                await _repository.SaveAsync();
                var result = _mapper.Map<ShiftRequestDtoGet>(entity);
                return ApiResponse<ShiftRequestDtoGet>.Success(result, "درخواست با موفقیت بررسی شد.");
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
                if (entity.Status != RequestStatus.Pending)
                    return ApiResponse<string>.Fail("امکان حذف این درخواست وجود ندارد.");
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

    }
}
