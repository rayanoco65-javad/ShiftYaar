using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel;
using ShiftYar.Application.Features.ShiftModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.ShiftModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftModel.Services
{
    public class ShiftService : IShiftService
    {
        private readonly IEfRepository<Shift> _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<ShiftService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ShiftService(IEfRepository<Shift> repository, IMapper mapper, ILogger<ShiftService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<ShiftDtoGet>>> GetShifts(ShiftFilter filter)
        {
            try
            {
                _logger.LogInformation("Getting filtered shifts");

                //var (items, totalCount) = await _repository.GetByFilterAsync(filter, "Department");
                var (items, totalCount) = await _repository.GetByFilterAsync(filter,
                    "Department",
                    "Department.Hospital",
                    "Department.Supervisor",
                    "RequiredSpecialties",
                    "RequiredSpecialties.Specialty");

                var shifts = items.Select(s => _mapper.Map<ShiftDtoGet>(s)).ToList();

                var pagedResponse = new PagedResponse<ShiftDtoGet>
                {
                    Items = shifts,
                    TotalCount = totalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
                };

                return ApiResponse<PagedResponse<ShiftDtoGet>>.Success(pagedResponse, "لیست شیفت‌ها با موفقیت دریافت شد.");
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت شیفت ها با خطا مواجه شد : " + ex.Message);
            }
        }

        public async Task<ApiResponse<ShiftDtoGet>> GetShift(int id)
        {
            try
            {
                _logger.LogInformation("Getting shift with ID: {Id}", id);

                var shift = await _repository.GetByIdAsync(id,
                    "Department",
                    "Department.Hospital",
                    "Department.Supervisor",
                    "RequiredSpecialties",
                    "RequiredSpecialties.Specialty");
                if (shift == null)
                {
                    _logger.LogWarning("Shift with ID {Id} not found", id);
                    return ApiResponse<ShiftDtoGet>.Fail("شیفت مورد نظر یافت نشد.");
                }

                var result = _mapper.Map<ShiftDtoGet>(shift);
                return ApiResponse<ShiftDtoGet>.Success(result, "شیفت با موفقیت دریافت شد.");
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس دریافت شیفت با خطا مواجه شد : " + ex.Message);
            }
        }

        public async Task<ApiResponse<ShiftDtoGet>> CreateShift(ShiftDtoAdd dto)
        {
            try
            {
                _logger.LogInformation("Creating new shift");

                var shift = _mapper.Map<Shift>(dto);

                shift.CreateDate = DateTime.Now;
                shift.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

                await _repository.AddAsync(shift);
                await _repository.SaveAsync();

                var result = await GetShift(shift.Id.Value);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس ایجاد شیفت با خطا مواجه شد : " + ex.Message);
            }
        }

        public async Task<ApiResponse<ShiftDtoGet>> UpdateShift(int id, ShiftDtoAdd dto)
        {
            try
            {
                _logger.LogInformation("Updating shift with ID: {Id}", id);

                var shift = await _repository.GetByIdAsync(id);
                if (shift == null)
                {
                    _logger.LogWarning("Shift update failed - Shift with ID {Id} not found", id);
                    return ApiResponse<ShiftDtoGet>.Fail("شیفت مورد نظر یافت نشد.");
                }

                _mapper.Map(dto, shift);

                shift.UpdateDate = DateTime.Now;
                shift.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

                _repository.Update(shift);
                await _repository.SaveAsync();

                var result = await GetShift(id);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس ویرایش شیفت با خطا مواجه شد : " + ex.Message);
            }
        }

        public async Task<ApiResponse<string>> DeleteShift(int id)
        {
            try
            {
                _logger.LogInformation("Deleting shift with ID: {Id}", id);

                var shift = await _repository.GetByIdAsync(id);
                if (shift == null)
                {
                    _logger.LogWarning("Shift deletion failed - Shift with ID {Id} not found", id);
                    return ApiResponse<string>.Fail("شیفت مورد نظر یافت نشد.");
                }

                _repository.Delete(shift);
                await _repository.SaveAsync();

                return ApiResponse<string>.Success("شیفت با موفقیت حذف شد.");
            }
            catch (Exception ex)
            {
                throw new Exception("سرویس حذف شیفت با خطا مواجه شد : " + ex.Message);
            }
        } 
    }
}
