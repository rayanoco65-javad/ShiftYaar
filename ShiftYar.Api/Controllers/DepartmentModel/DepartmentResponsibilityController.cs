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
    [ApiController]
    [Route("api/[controller]")]
    [Route("[controller]")]
    public class DepartmentResponsibilityController : BaseController
    {
        private readonly IDepartmentResponsibilityService _responsibilityService;

        public DepartmentResponsibilityController(ShiftYarDbContext context, IDepartmentResponsibilityService responsibilityService) : base(context)
        {
            _responsibilityService = responsibilityService;
        }

        /// <summary>دریافت تمام مسئولیت‌های یک بخش بر اساس شناسه دپارتمان</summary>
        [HttpGet("by-department/{departmentId:int}")]
        [HttpGet("department/{departmentId:int}")]
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

        /// <summary>دریافت تمام مسئولیت‌های یک بخش از طریق Query Parameter</summary>
        [HttpGet("by-department")]
        [HttpGet("department")]
        public async Task<ActionResult<ApiResponse<List<DepartmentResponsibilityDtoGet>>>> GetByDepartmentQuery([FromQuery] int departmentId)
        {
            return await GetByDepartmentId(departmentId);
        }

        /// <summary>دریافت یک مسئولیت با شناسه</summary>
        [HttpGet("{id:int}")]
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
        [HttpPost("create")]
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
        [HttpPut("{id:int}")]
        [HttpPut]
        public async Task<ActionResult<ApiResponse<DepartmentResponsibilityDtoGet>>> Update([FromRoute] int? id, [FromBody] DepartmentResponsibilityDtoUpdate dto)
        {
            try
            {
                int targetId = id ?? dto?.Id ?? 0;
                if (targetId <= 0)
                {
                    return BadRequest(ApiResponse<DepartmentResponsibilityDtoGet>.Fail("شناسه مسئولیت الزامی است."));
                }

                var result = await _responsibilityService.UpdateAsync(targetId, dto!);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<DepartmentResponsibilityDtoGet>.Fail("خطا در ویرایش مسئولیت: " + ex.Message));
            }
        }

        /// <summary>حذف مسئولیت</summary>
        [HttpDelete("{id:int}")]
        [HttpDelete]
        public async Task<ActionResult<ApiResponse<string>>> Delete([FromRoute] int? id, [FromQuery] int? itemId)
        {
            int targetId = id ?? itemId ?? 0;
            if (targetId <= 0)
            {
                return BadRequest(ApiResponse<string>.Fail("شناسه مسئولیت الزامی است."));
            }

            try
            {
                var result = await _responsibilityService.DeleteAsync(targetId);
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
        [HttpGet("staff-matrix/{departmentId:int}")]
        [HttpGet("matrix/{departmentId:int}")]
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

        /// <summary>دریافت ماتریس پرسنل و مسئولیت‌های بخش از طریق Query Parameter</summary>
        [HttpGet("staff-matrix")]
        [HttpGet("matrix")]
        public async Task<ActionResult<ApiResponse<DepartmentStaffMatrixDto>>> GetStaffMatrixQuery([FromQuery] int departmentId)
        {
            return await GetStaffMatrix(departmentId);
        }

        /// <summary>ایجاد سریع نقش‌های استاندارد اتاق عمل برای بخش (سیرکولر، اسکراب اول، اسکراب دوم، اد، وینیست)</summary>
        [HttpPost("seed-operating-room/{departmentId:int}")]
        [HttpPost("seed-operating-room")]
        public async Task<ActionResult<ApiResponse<List<DepartmentResponsibilityDtoGet>>>> SeedOperatingRoom([FromRoute] int? departmentId, [FromQuery] int? deptId)
        {
            int targetDeptId = departmentId ?? deptId ?? 0;
            if (targetDeptId <= 0)
            {
                return BadRequest(ApiResponse<List<DepartmentResponsibilityDtoGet>>.Fail("شناسه بخش نامعتبر است."));
            }

            try
            {
                var result = await _responsibilityService.SeedOperatingRoomAsync(targetDeptId);
                if (!result.IsSuccess) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<DepartmentResponsibilityDtoGet>>.Fail("خطا در ایجاد نقش‌های پیش‌فرض اتاق عمل: " + ex.Message));
            }
        }
    }
}
