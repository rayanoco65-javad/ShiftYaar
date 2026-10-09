using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel.DepartmentResponsibilityModel;
using ShiftYar.Application.Interfaces.DepartmentModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Api.Controllers.DepartmentModel
{
    public class DepartmentResponsibilityController : BaseController
    {
        private readonly IDepartmentResponsibilityService _responsibilityService;

        public DepartmentResponsibilityController(ShiftYarDbContext context, IDepartmentResponsibilityService responsibilityService) : base(context)
        {
            _responsibilityService = responsibilityService;
        }

        /// <summary>دریافت تمام مسئولیت‌های یک بخش</summary>
        [HttpGet("department/{departmentId}")]
        public async Task<ActionResult<ApiResponse<List<DepartmentResponsibilityDtoGet>>>> GetByDepartmentId(int departmentId)
        {
            try
            {
                var result = await _responsibilityService.GetByDepartmentIdAsync(departmentId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<DepartmentResponsibilityDtoGet>>.Fail("خطا در دریافت مسئولیت‌های بخش: " + ex.Message));
            }
        }

        /// <summary>دریافت یک مسئولیت با شناسه</summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<DepartmentResponsibilityDtoGet>>> GetById(int id)
        {
            try
            {
                var result = await _responsibilityService.GetByIdAsync(id);
                if (!result.IsSuccess) return NotFound(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<DepartmentResponsibilityDtoGet>.Fail("خطا در دریافت مسئولیت: " + ex.Message));
            }
        }

        /// <summary>ایجاد مسئولیت جدید در بخش</summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<DepartmentResponsibilityDtoGet>>> Create([FromBody] DepartmentResponsibilityDtoAdd dto)
        {
            try
            {
                var result = await _responsibilityService.CreateAsync(dto);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<DepartmentResponsibilityDtoGet>.Fail("خطا در ایجاد مسئولیت: " + ex.Message));
            }
        }

        /// <summary>ویرایش مسئولیت در بخش</summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<DepartmentResponsibilityDtoGet>>> Update(int id, [FromBody] DepartmentResponsibilityDtoUpdate dto)
        {
            try
            {
                var result = await _responsibilityService.UpdateAsync(id, dto);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<DepartmentResponsibilityDtoGet>.Fail("خطا در ویرایش مسئولیت: " + ex.Message));
            }
        }

        /// <summary>حذف مسئولیت</summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> Delete(int id)
        {
            try
            {
                var result = await _responsibilityService.DeleteAsync(id);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail("خطا در حذف مسئولیت: " + ex.Message));
            }
        }

        /// <summary>تخصیص مسئولیت‌ها به یک کاربر</summary>
        [HttpPost("assign-user")]
        public async Task<ActionResult<ApiResponse<string>>> AssignUserResponsibilities([FromBody] UserResponsibilityAssignDto dto)
        {
            try
            {
                var result = await _responsibilityService.AssignUserResponsibilitiesAsync(dto);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail("خطا در تخصیص مسئولیت به کاربر: " + ex.Message));
            }
        }

        /// <summary>تخصیص گروهی مسئولیت‌ها به پرسنل بخش</summary>
        [HttpPost("batch-assign")]
        public async Task<ActionResult<ApiResponse<string>>> BatchAssign([FromBody] BatchAssignResponsibilitiesDto dto)
        {
            try
            {
                var result = await _responsibilityService.BatchAssignResponsibilitiesAsync(dto);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail("خطا در تخصیص گروهی مسئولیت‌ها: " + ex.Message));
            }
        }

        /// <summary>دریافت ماتریس پرسنل و مسئولیت‌های بخش جهت نمایش در فرانت‌اند</summary>
        [HttpGet("matrix/{departmentId}")]
        public async Task<ActionResult<ApiResponse<DepartmentStaffMatrixDto>>> GetStaffMatrix(int departmentId)
        {
            try
            {
                var result = await _responsibilityService.GetStaffMatrixAsync(departmentId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<DepartmentStaffMatrixDto>.Fail("خطا در دریافت ماتریس پرسنل بخش: " + ex.Message));
            }
        }
    }
}
