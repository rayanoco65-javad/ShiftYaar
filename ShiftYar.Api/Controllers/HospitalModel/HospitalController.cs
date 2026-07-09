using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Api.Controllers.UserModel;
using ShiftYar.Api.Filters;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.HospitalModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.HospitalModel.Filters;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Interfaces.HospitalModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.HospitalModel
{
    //[Authorize]
    //[HasPermission("ManageUsers")]
    //[RequireRole("Admin")]
    public class HospitalController : BaseController
    {
        private readonly IHospitalService _hospitalService;
        private readonly ILogger<HospitalController> _logger;

        public HospitalController(ShiftYarDbContext context, IHospitalService hospitalService, ILogger<HospitalController> logger) : base(context)
        {
            _hospitalService = hospitalService;
            _logger = logger;
        }

        //دریافت لیست بیمارستان ها
        //[Authorize]                      //میتونیم در کل کنترلر یا فقط در یک یا چند اکشن مشخص از این فیلتر استفاده کنیم
        //[HasPermission("ManageUsers")]   //میتونیم در کل کنترلر یا فقط در یک یا چند اکشن مشخص از این فیلتر استفاده کنیم
        [HttpGet]
        //[ServiceFilter(typeof(RequestLoggingFilter))]     //میتونیم در کل کنترلر یا فقط در یک یا چند اکشن مشخص از این فیلتر استفاده کنیم
        public async Task<IActionResult> GetHospitals([FromQuery] HospitalFilter filter)
        {
            var result = await _hospitalService.GetFilteredUsersAsync(filter);
            return Ok(result);
        }

        // دریافت یک بیمارستان خاص
        [HttpGet]
        public async Task<IActionResult> GetHospital(int id)
        {
            var result = await _hospitalService.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result) : NotFound(result);
        }

        ///افزودن بیمارستان جدید
        [HttpPost]
        public async Task<IActionResult> CreateHospital([FromBody] HospitalDtoAdd dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<string>.Fail("ورودی نامعتبر است."));

            _logger.LogInformation("در حال ایجاد بیمارستان با سیام {SiamCode} توسط {User}", dto.SiamCode, User.Identity?.Name);

            var result = await _hospitalService.CreateAsync(dto);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }


        ///ویرایش بیمارستان
        [HttpPut]
        public async Task<IActionResult> UpdateHospital(int id, [FromBody] HospitalDtoAdd dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<string>.Fail("ورودی نامعتبر است."));

            _logger.LogInformation("در حال ویرایش بیمارستان {Id} توسط {User}", id, User.Identity?.Name);

            var result = await _hospitalService.UpdateAsync(id, dto);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }


        ///حذف بیمارستان
        [HttpDelete]
        public async Task<IActionResult> DeleteHospital(int id)
        {
            _logger.LogWarning("در حال حذف بیمارستان {Id} توسط {User}", id, User.Identity?.Name);

            var result = await _hospitalService.DeleteAsync(id);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

    }
}
