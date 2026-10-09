using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel.DepartmentResponsibilityModel;
using ShiftYar.Application.Interfaces.DepartmentModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.DepartmentModel.Services
{
    public class DepartmentResponsibilityService : IDepartmentResponsibilityService
    {
        private readonly IEfRepository<DepartmentResponsibility> _respRepo;
        private readonly IEfRepository<UserDepartmentResponsibility> _userRespRepo;
        private readonly IEfRepository<User> _userRepo;
        private readonly IEfRepository<Department> _deptRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<DepartmentResponsibilityService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DepartmentResponsibilityService(
            IEfRepository<DepartmentResponsibility> respRepo,
            IEfRepository<UserDepartmentResponsibility> userRespRepo,
            IEfRepository<User> userRepo,
            IEfRepository<Department> deptRepo,
            IMapper mapper,
            ILogger<DepartmentResponsibilityService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _respRepo = respRepo;
            _userRespRepo = userRespRepo;
            _userRepo = userRepo;
            _deptRepo = deptRepo;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        public async Task<ApiResponse<List<DepartmentResponsibilityDtoGet>>> GetByDepartmentIdAsync(int departmentId)
        {
            _logger.LogInformation("Fetching responsibilities for department {DepartmentId}", departmentId);
            var (items, _) = await _respRepo.GetByFilterAsync(
                new Common.Filters.SimpleFilter<DepartmentResponsibility>(r => r.DepartmentId == departmentId && (r.IsActive ?? true)),
                "Specialty", "UserResponsibilities");

            var dtos = _mapper.Map<List<DepartmentResponsibilityDtoGet>>(items.OrderBy(r => r.Priority ?? 0));
            return ApiResponse<List<DepartmentResponsibilityDtoGet>>.Success(dtos);
        }

        public async Task<ApiResponse<DepartmentResponsibilityDtoGet>> GetByIdAsync(int id)
        {
            var entity = await _respRepo.GetByIdAsync(id, "Specialty", "UserResponsibilities");
            if (entity == null)
            {
                return ApiResponse<DepartmentResponsibilityDtoGet>.Fail("مسئولیت یافت نشد.");
            }

            var dto = _mapper.Map<DepartmentResponsibilityDtoGet>(entity);
            return ApiResponse<DepartmentResponsibilityDtoGet>.Success(dto);
        }

        public async Task<ApiResponse<DepartmentResponsibilityDtoGet>> CreateAsync(DepartmentResponsibilityDtoAdd dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return ApiResponse<DepartmentResponsibilityDtoGet>.Fail("عنوان مسئولیت الزامی است.");
            }

            var dept = await _deptRepo.GetByIdAsync(dto.DepartmentId);
            if (dept == null)
            {
                return ApiResponse<DepartmentResponsibilityDtoGet>.Fail("بخش یافت نشد.");
            }

            var entity = _mapper.Map<DepartmentResponsibility>(dto);
            entity.CreateDate = DateTime.Now;
            entity.TheUserId = GetCurrentUserId();
            entity.IsActive = true;

            await _respRepo.AddAsync(entity);
            await _respRepo.SaveAsync();

            var resultDto = _mapper.Map<DepartmentResponsibilityDtoGet>(entity);
            return ApiResponse<DepartmentResponsibilityDtoGet>.Success(resultDto, "مسئولیت با موفقیت ایجاد شد.");
        }

        public async Task<ApiResponse<DepartmentResponsibilityDtoGet>> UpdateAsync(int id, DepartmentResponsibilityDtoUpdate dto)
        {
            var entity = await _respRepo.GetByIdAsync(id);
            if (entity == null)
            {
                return ApiResponse<DepartmentResponsibilityDtoGet>.Fail("مسئولیت یافت نشد.");
            }

            entity.Title = dto.Title;
            entity.Description = dto.Description;
            entity.SpecialtyId = dto.SpecialtyId;
            entity.IsDefault = dto.IsDefault ?? entity.IsDefault;
            entity.Priority = dto.Priority ?? entity.Priority;
            if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;

            entity.UpdateDate = DateTime.Now;
            entity.TheUserId = GetCurrentUserId();

            _respRepo.Update(entity);
            await _respRepo.SaveAsync();

            var resultDto = _mapper.Map<DepartmentResponsibilityDtoGet>(entity);
            return ApiResponse<DepartmentResponsibilityDtoGet>.Success(resultDto, "مسئولیت با موفقیت ویرایش شد.");
        }

        public async Task<ApiResponse<string>> DeleteAsync(int id)
        {
            var entity = await _respRepo.GetByIdAsync(id);
            if (entity == null)
            {
                return ApiResponse<string>.Fail("مسئولیت یافت نشد.");
            }

            _respRepo.Delete(entity);
            await _respRepo.SaveAsync();
            return ApiResponse<string>.Success(null, "مسئولیت با موفقیت حذف شد.");
        }

        public async Task<ApiResponse<string>> AssignUserResponsibilitiesAsync(UserResponsibilityAssignDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId, "UserResponsibilities");
            if (user == null)
            {
                return ApiResponse<string>.Fail("کاربر یافت نشد.");
            }

            var currentUserId = GetCurrentUserId();
            var (existingLinks, _) = await _userRespRepo.GetByFilterAsync(
                new Common.Filters.SimpleFilter<UserDepartmentResponsibility>(r => r.UserId == dto.UserId));

            // حذف مواردی که در لیست جدید نیستند
            foreach (var link in existingLinks)
            {
                if (!dto.ResponsibilityIds.Contains(link.DepartmentResponsibilityId ?? 0))
                {
                    _userRespRepo.Delete(link);
                }
            }

            // اضافه کردن موارد جدید
            var existingRespIds = existingLinks
                .Where(l => l.DepartmentResponsibilityId.HasValue)
                .Select(l => l.DepartmentResponsibilityId!.Value)
                .ToHashSet();

            foreach (var respId in dto.ResponsibilityIds.Distinct())
            {
                if (!existingRespIds.Contains(respId))
                {
                    await _userRespRepo.AddAsync(new UserDepartmentResponsibility
                    {
                        UserId = dto.UserId,
                        DepartmentResponsibilityId = respId,
                        CreateDate = DateTime.Now,
                        TheUserId = currentUserId
                    });
                }
            }

            await _userRespRepo.SaveAsync();
            return ApiResponse<string>.Success(null, "مسئولیت‌های کاربر با موفقیت بروزرسانی شد.");
        }

        public async Task<ApiResponse<string>> BatchAssignResponsibilitiesAsync(BatchAssignResponsibilitiesDto dto)
        {
            foreach (var item in dto.Assignments)
            {
                await AssignUserResponsibilitiesAsync(item);
            }
            return ApiResponse<string>.Success(null, "تخصیص گروهی مسئولیت‌ها با موفقیت ذخیره شد.");
        }

        public async Task<ApiResponse<DepartmentStaffMatrixDto>> GetStaffMatrixAsync(int departmentId)
        {
            var (respList, _) = await _respRepo.GetByFilterAsync(
                new Common.Filters.SimpleFilter<DepartmentResponsibility>(r => r.DepartmentId == departmentId && (r.IsActive ?? true)),
                "Specialty");

            var (users, _) = await _userRepo.GetByFilterAsync(
                new Common.Filters.SimpleFilter<User>(u => u.DepartmentId == departmentId && (u.IsActive ?? true)),
                "Specialty", "UserResponsibilities");

            var matrix = new DepartmentStaffMatrixDto
            {
                DepartmentId = departmentId,
                Responsibilities = _mapper.Map<List<DepartmentResponsibilityDtoGet>>(respList.OrderBy(r => r.Priority ?? 0)),
                Staff = users.Select(u => new StaffResponsibilityMatrixItemDto
                {
                    UserId = u.Id ?? 0,
                    FullName = u.FullName ?? "",
                    PersonnelCode = u.PersonnelCode,
                    SpecialtyId = u.SpecialtyId,
                    SpecialtyName = u.Specialty?.SpecialtyName,
                    AssignedResponsibilityIds = u.UserResponsibilities?
                        .Where(ur => ur.DepartmentResponsibilityId.HasValue)
                        .Select(ur => ur.DepartmentResponsibilityId!.Value)
                        .ToList() ?? new List<int>()
                }).OrderBy(s => s.FullName).ToList()
            };

            return ApiResponse<DepartmentStaffMatrixDto>.Success(matrix);
        }
    }
}
