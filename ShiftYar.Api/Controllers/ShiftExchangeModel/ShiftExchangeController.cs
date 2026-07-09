using Microsoft.AspNetCore.Mvc;
using ShiftYar.Api.Controllers;
using ShiftYar.Application.DTOs.ShiftExchangeModel;
using ShiftYar.Application.Interfaces;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftYar.Api.Controllers.ShiftExchangeModel
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShiftExchangeController : BaseController
    {
        private readonly IShiftExchangeService _shiftExchangeService;

        public ShiftExchangeController(ShiftYarDbContext context, IShiftExchangeService shiftExchangeService) : base(context)
        {
            _shiftExchangeService = shiftExchangeService;
        }

        /// <summary>
        /// دریافت تمام درخواست‌های جابجایی شیفت
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _shiftExchangeService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// دریافت درخواست جابجایی شیفت بر اساس شناسه
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _shiftExchangeService.GetByIdAsync(id);
                if (result == null)
                {
                    return NotFound("درخواست جابجایی شیفت یافت نشد");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// دریافت درخواست‌های جابجایی شیفت کاربر
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUserId(int userId)
        {
            try
            {
                var result = await _shiftExchangeService.GetByUserIdAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// دریافت درخواست‌های در انتظار تأیید سوپروایزر
        /// </summary>
        [HttpGet("pending-approvals/{supervisorId}")]
        public async Task<IActionResult> GetPendingApprovals(int supervisorId)
        {
            try
            {
                var result = await _shiftExchangeService.GetPendingApprovalsAsync(supervisorId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// ایجاد درخواست جابجایی شیفت جدید
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ShiftExchangeDtoAdd dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _shiftExchangeService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// ویرایش درخواست جابجایی شیفت
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ShiftExchangeDtoUpdate dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _shiftExchangeService.UpdateAsync(dto);
                if (result == null)
                {
                    return NotFound("درخواست جابجایی شیفت یافت نشد");
                }
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// تأیید یا رد درخواست جابجایی شیفت توسط سوپروایزر
        /// </summary>
        [HttpPost("approve")]
        public async Task<IActionResult> Approve([FromBody] ShiftExchangeApprovalDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _shiftExchangeService.ApproveAsync(dto);
                if (!result)
                {
                    return NotFound("درخواست جابجایی شیفت یافت نشد");
                }
                return Ok(new { message = "درخواست با موفقیت " + (dto.IsApproved ? "تأیید" : "رد") + " شد" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// اجرای جابجایی شیفت (پس از تأیید)
        /// </summary>
        [HttpPost("execute/{exchangeId}")]
        public async Task<IActionResult> ExecuteExchange(int exchangeId)
        {
            try
            {
                var result = await _shiftExchangeService.ExecuteExchangeAsync(exchangeId);
                if (!result)
                {
                    return NotFound("درخواست جابجایی شیفت یافت نشد");
                }
                return Ok(new { message = "جابجایی شیفت با موفقیت اجرا شد" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// لغو درخواست جابجایی شیفت
        /// </summary>
        [HttpPost("cancel/{exchangeId}")]
        public async Task<IActionResult> Cancel(int exchangeId)
        {
            try
            {
                var result = await _shiftExchangeService.CancelAsync(exchangeId);
                if (!result)
                {
                    return NotFound("درخواست جابجایی شیفت یافت نشد");
                }
                return Ok(new { message = "درخواست جابجایی شیفت با موفقیت لغو شد" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// حذف درخواست جابجایی شیفت
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _shiftExchangeService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound("درخواست جابجایی شیفت یافت نشد");
                }
                return Ok(new { message = "درخواست جابجایی شیفت با موفقیت حذف شد" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
