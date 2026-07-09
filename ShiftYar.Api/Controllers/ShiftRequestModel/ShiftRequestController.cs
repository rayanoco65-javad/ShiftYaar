using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.DTOs.ShiftModel.ShiftRequestModel;
using ShiftYar.Application.Features.ShiftRequestModel.Filters;
using ShiftYar.Application.Interfaces.ShiftRequestModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.ShiftRequestModel
{
    public class ShiftRequestController : BaseController
    {
        private readonly IShiftRequestService _service;
        public ShiftRequestController(ShiftYarDbContext context, IShiftRequestService service) : base(context)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetShiftRequest(int id)
        {
            try
            {
                var result = await _service.GetShiftRequestAsync(id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات دریافت درخواست شیفت‌ با خطا مواجه شد : " + ex.Message);
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetShiftRequests([FromQuery] ShiftRequestFilter filter)
        {
            try
            {
                var result = await _service.GetShiftRequestsAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات دریافت درخواست های شیفت‌ با خطا مواجه شد : " + ex.Message);
            }
        }


        [HttpPost]
        public async Task<IActionResult> CreateShiftRequest([FromBody] ShiftRequestDtoAdd dto)
        {
            try
            {
                var result = await _service.CreateShiftRequestAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ثبت درخواست های شیفت‌ با خطا مواجه شد : " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateShiftRequestForLeave([FromBody] ShiftRequestForLeaveDtoAdd dto)
        {
            try
            {
                var result = await _service.CreateShiftRequestForLeaveAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ثبت درخواست مرخصی با خطا مواجه شد : " + ex.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateShiftRequestByUser(int id, [FromBody] ShiftRequestDtoAdd dto)
        {
            try
            {
                var result = await _service.UpdateShiftRequestByUserAsync(id, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ویرایش درخواست شیفت‌ با خطا مواجه شد : " + ex.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateShiftRequestBySupervisor(int id, [FromBody] ShiftRequestDtoUpdateBySupervisor dto)
        {
            try
            {
                var result = await _service.UpdateShiftRequestBySupervisorAsync(id, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات ویرایش درخواست شیفت‌ با خطا مواجه شد : " + ex.Message);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteShiftRequest(int id)
        {
            try
            {
                var result = await _service.DeleteShiftRequestAsync(id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("عملیات حذف درخواست شیفت‌ با خطا مواجه شد : " + ex.Message);
            }
        }

    }
}
