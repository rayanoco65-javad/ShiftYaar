using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequiredResponsibilityModel;
using ShiftYar.Application.Interfaces.ShiftRequiredResponsibilityModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Api.Controllers.ShiftModel
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShiftRequiredResponsibilityController : BaseController
    {
        private readonly IShiftRequiredResponsibilityService _responsibilityService;

        public ShiftRequiredResponsibilityController(ShiftYarDbContext context, IShiftRequiredResponsibilityService responsibilityService) : base(context)
        {
            _responsibilityService = responsibilityService;
        }

        /// <summary>دریافت نیازمندی‌های مسئولیت برای یک شیفت مشخص</summary>
        [HttpGet("by-shift/{shiftId:int}")]
        public async Task<ActionResult<ApiResponse<List<ShiftRequiredResponsibilityDtoGet>>>> GetByShiftId(int shiftId)
        {
            try
            {
                var result = await _responsibilityService.GetByShiftIdAsync(shiftId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<ShiftRequiredResponsibilityDtoGet>>.Fail("خطا در دریافت نیازمندی‌های مسئولیت شیفت: " + ex.Message));
            }
        }

        /// <summary>دریافت یک نیازمندی مسئولیت با شناسه</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ShiftRequiredResponsibilityDtoGet>>> GetById(int id)
        {
            try
            {
                var result = await _responsibilityService.GetByIdAsync(id);
                if (!result.IsSuccess) return NotFound(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail("خطا در دریافت نیازمندی مسئولیت: " + ex.Message));
            }
        }

        /// <summary>ایجاد نیازمندی مسئولیت برای شیفت</summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<ShiftRequiredResponsibilityDtoGet>>> Create([FromBody] ShiftRequiredResponsibilityDtoAdd dto)
        {
            try
            {
                var result = await _responsibilityService.CreateAsync(dto);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail("خطا در ایجاد نیازمندی مسئولیت: " + ex.Message));
            }
        }

        /// <summary>ویرایش نیازمندی مسئولیت شیفت</summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<ShiftRequiredResponsibilityDtoGet>>> Update(int id, [FromBody] ShiftRequiredResponsibilityDtoAdd dto)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail("شناسه نیازمندی الزامی است."));
                }

                if (dto != null && (!dto.Id.HasValue || dto.Id.Value <= 0))
                {
                    dto.Id = id;
                }

                var result = await _responsibilityService.UpdateAsync(id, dto!);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ShiftRequiredResponsibilityDtoGet>.Fail("خطا در ویرایش نیازمندی مسئولیت: " + ex.Message));
            }
        }

        /// <summary>حذف نیازمندی مسئولیت شیفت</summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<string>>> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<string>.Fail("شناسه نیازمندی الزامی است."));
            }

            try
            {
                var result = await _responsibilityService.DeleteAsync(id);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail("خطا در حذف نیازمندی مسئولیت: " + ex.Message));
            }
        }
    }
}
