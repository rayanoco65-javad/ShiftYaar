using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Features.UserModel.Services;
using ShiftYar.Application.Interfaces.Security;
using ShiftYar.Application.Interfaces.UserModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.UserModel
{
    public class UserController : BaseController
    {
        private readonly IJwtService _jwtService;
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(ShiftYarDbContext context, IJwtService jwtService, IUserService userService, ILogger<UserController> logger) : base(context)
        {
            _jwtService = jwtService;
            _userService = userService;
            _logger = logger;
        }

        // دریافت لیست کاربران با فیلتر
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] UserFilter filter)
        {
            var result = await _userService.GetFilteredUsersAsync(filter);
            return Ok(result);
        }

        // دریافت کاربر خاص
        [HttpGet]
        public async Task<IActionResult> GetUser(int id)
        {
            var result = await _userService.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result) : NotFound(result);
        }


        // افزودن کاربر جدید
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] UserDtoAdd dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ApiResponse<string>.Fail("ورودی نامعتبر است."));

                _logger.LogInformation("در حال ایجاد کاربر با کدملی {NationalCode} توسط {User}", dto.NationalCode, User.Identity?.Name);

                var result = await _userService.CreateAsync(dto);
                return result.IsSuccess ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {

                return BadRequest(ApiResponse<string>.Fail("عملیات ایجاد کاربر با خطا مواجه شد : " + ex.Message));
            }
        }


        /// ایجاد کاربر با نقش سوپروایزر و ارسال OTP جهت لاگین
        [HttpPost]
        public async Task<IActionResult> CreateUserSupervisorAndSendOtpForLogin([FromBody] UserDtoAdd dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ApiResponse<string>.Fail("ورودی نامعتبر است."));

                var result = await _userService.CreateSupervisorAndSendOtpAsync(dto);
                return result.IsSuccess ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در CreateUser_Supervisor_Init");
                return BadRequest(ApiResponse<string>.Fail("عملیات با خطا مواجه شد : " + ex.Message));
            }
        }


        // ویرایش کاربر
        [HttpPut]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserDtoAdd dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<string>.Fail("ورودی نامعتبر است."));

            _logger.LogInformation("در حال ویرایش کاربر {Id} توسط {User}", id, User.Identity?.Name);

            var result = await _userService.UpdateAsync(id, dto);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        // حذف کاربر
        [HttpDelete]
        public async Task<IActionResult> DeleteUser(int id)
        {
            _logger.LogWarning("در حال حذف کاربر {Id} توسط {User}", id, User.Identity?.Name);

            var result = await _userService.DeleteAsync(id);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

    }
}
