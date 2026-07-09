using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Interfaces.Security;
using ShiftYar.Domain.Entities.SecurityModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;

namespace ShiftYar.Api.Controllers.AuthModel
{
    [Route("[action]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> LoginWithPassword([FromBody] LoginWithPasswordRequestDto dto)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var device = Request.Headers["User-Agent"].ToString();
                var result = await _authService.LoginWithPasswordAsync(dto, ip, device);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { status = 401, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در LoginWithPassword");
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendOtp_Login([FromBody] SendOtpRequestDto dto)
        {
            try
            {
                await _authService.SendOtpAsync_Login(dto);
                return Ok(new { status = 200, message = "کد با موفقیت ارسال شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در SendOtp");
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> LoginWithOtp([FromBody] LoginWithOtpRequestDto dto)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var device = Request.Headers["User-Agent"].ToString();
                var result = await _authService.LoginWithOtpAsync(dto, ip, device);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { status = 401, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در LoginWithOtp");
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendOtp_ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
        {
            try
            {
                await _authService.SendOtp_ForgotPasswordAsync(dto);
                return Ok(new { status = 200, message = "کد بازیابی ارسال شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ForgotPassword");
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
        {
            try
            {
                await _authService.ResetPasswordAsync(dto);
                return Ok(new { status = 200, message = "رمز عبور با موفقیت تغییر کرد." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { status = 401, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ResetPassword");
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        {
            try
            {
                var newAccessToken = await _authService.RefreshTokenAsync(refreshToken);
                return Ok(new { Token = newAccessToken });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { status = 401, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در RefreshToken");
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout([FromBody] string refreshToken)
        {
            try
            {
                await _authService.LogoutAsync(refreshToken);
                return Ok(new { message = "خروج با موفقیت انجام شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در Logout");
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> LogoutAll([FromBody] string phoneNumberMembership)
        {
            try
            {
                await _authService.LogoutAllAsync(phoneNumberMembership);
                return Ok(new { status = 200, message = "تمام توکن‌ها باطل شدند." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return NotFound(new { status = 404, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در LogoutAll");
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }


        //private readonly IJwtService _jwtService;
        //private readonly ShiftYarDbContext _context;
        //private readonly ILogger<AuthController> _logger;

        //public AuthController(IJwtService jwtService, ShiftYarDbContext context, ILogger<AuthController> logger)
        //{
        //    _jwtService = jwtService;
        //    _context = context;
        //    _logger = logger;
        //}

        //[HttpPost]
        //public async Task<IActionResult> Login([FromBody] LoginRequestDto login)
        //{
        //    try
        //    {
        //        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        //        var device = Request.Headers["User-Agent"].ToString();

        //        _logger.LogInformation(
        //            "تلاش برای ورود | شماره تلفن: {PhoneNumber} | IP: {IpAddress} | دستگاه: {Device}",
        //            login.PhoneNumberMembership, ipAddress, device
        //        );

        //        var user = await _context.Users
        //        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
        //        .Include(u => u.RefreshTokens)
        //        .Include(u => u.LoginHistories)
        //        .SingleOrDefaultAsync(x => x.PhoneNumberMembership == login.PhoneNumberMembership);

        //        if (user == null || !BCrypt.Net.BCrypt.Verify(login.Password, user.Password))
        //        {
        //            _logger.LogWarning(
        //                "ورود ناموفق | شماره تلفن: {PhoneNumber} | IP: {IpAddress} | دستگاه: {Device}",
        //                login.PhoneNumberMembership, ipAddress, device
        //            );
        //            return Unauthorized(new { status = 401, message = "نام کاربری یا رمز عبور اشتباه است." });
        //        }

        //        //دریافت نقش های کاربر
        //        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        //        //دریافت مجوزهای کاربر
        //        var permissions = user.UserRoles
        //            .SelectMany(ur => ur.Role.RolePermissions)
        //            .Select(rp => rp.Permission.Name)
        //            .Distinct()
        //            .ToList();

        //        var accessToken = _jwtService.GenerateAccessToken(user, roles, permissions);
        //        var refreshToken = _jwtService.GenerateRefreshToken();

        //        user.RefreshTokens.Add(new RefreshToken
        //        {
        //            Token = refreshToken,
        //            Expires = DateTime.UtcNow.AddDays(7),
        //            IsRevoked = false
        //        });

        //        user.LoginHistories.Add(new LoginHistory
        //        {
        //            LoginTime = DateTime.UtcNow,
        //            IPAddress = ipAddress,
        //            Device = device
        //        });

        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation(
        //            "ورود موفق | کاربر: {UserName} (ID: {UserId}) | نقش‌ها: {Roles} | IP: {IpAddress} | دستگاه: {Device}",
        //            user.FullName, user.Id, string.Join(", ", roles), ipAddress, device
        //        );

        //        return Ok(new LoginResponseDto
        //        {
        //            Token = accessToken,
        //            RefreshToken = refreshToken,
        //            Roles = roles,
        //            User = user
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(
        //            ex,
        //            "خطا در عملیات ورود | شماره تلفن: {PhoneNumber} | IP: {IpAddress} | دستگاه: {Device}",
        //            login.PhoneNumberMembership,
        //            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
        //            Request.Headers["User-Agent"].ToString()
        //        );
        //        return BadRequest(new { status = 400, message = "عملیات با خطا مواجه شد : " + ex.Message });
        //    }
        //}

        //[HttpPost]
        //public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        //{
        //    try
        //    {
        //        var user = await _context.Users
        //        .Include(u => u.RefreshTokens)
        //        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
        //        .SingleOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken && !(bool)rt.IsRevoked && rt.Expires > DateTime.UtcNow));

        //        if (user == null)
        //            return Unauthorized(new { status = 401, message = "RefreshToken نامعتبر است یا منقضی شده است" });

        //        //دریافت نقش های کاربر
        //        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        //        //دریافت مجوزهای کاربر
        //        var permissions = user.UserRoles
        //            .SelectMany(ur => ur.Role.RolePermissions)
        //            .Select(rp => rp.Permission.Name)
        //            .Distinct()
        //            .ToList();

        //        var newAccessToken = _jwtService.GenerateAccessToken(user, roles, permissions);

        //        return Ok(new { Token = newAccessToken });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { status = 400, message = "عملیات با خطا مواجه شد : " + ex.Message });
        //    }
        //}

        //[HttpPost]
        //public async Task<IActionResult> Logout([FromBody] string refreshToken)
        //{
        //    try
        //    {
        //        var user = await _context.Users
        //            .Include(u => u.RefreshTokens)
        //            .SingleOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken));

        //        if (user != null)
        //        {
        //            var token = user.RefreshTokens.FirstOrDefault(rt => rt.Token == refreshToken);
        //            if (token != null)
        //            {
        //                token.IsRevoked = true;
        //                await _context.SaveChangesAsync();

        //                _logger.LogInformation(
        //                    "خروج موفق | کاربر: {UserName} (ID: {UserId}) | IP: {IpAddress} | دستگاه: {Device}",
        //                    user.FullName,
        //                    user.Id,
        //                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
        //                    Request.Headers["User-Agent"].ToString()
        //                );
        //            }
        //        }

        //        return Ok(new { message = "خروج با موفقیت انجام شد" });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(
        //            ex,
        //            "خطا در عملیات خروج | توکن: {RefreshToken} | IP: {IpAddress} | دستگاه: {Device}",
        //            refreshToken,
        //            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
        //            Request.Headers["User-Agent"].ToString()
        //        );
        //        return BadRequest(new { status = 400, message = "عملیات با خطا مواجه شد : " + ex.Message });
        //    }
        //}

        //[HttpPost]
        //public async Task<IActionResult> LogoutAll([FromBody] string phoneNumberMembership)
        //{
        //    try
        //    {
        //        var user = _context.Users
        //        .Include(u => u.RefreshTokens)
        //        .SingleOrDefault(x => x.PhoneNumberMembership == phoneNumberMembership);

        //        if (user == null)
        //            return NotFound(new { status = 404, message = "کاربر یافت نشد." });

        //        foreach (var token in user.RefreshTokens)
        //        {
        //            token.IsRevoked = true;
        //        }

        //        await _context.SaveChangesAsync();
        //        return Ok(new { status = 200, message = "تمام توکن‌ها باطل شدند." });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { status = 400, message = "عملیات با خطا مواجه شد : " + ex.Message });
        //    }
        //}
    }

}


// حالا قابلیت‌های زیر پیاده‌سازی شده‌اند:
// - ذخیره تاریخچه ورود کاربر
// - ذخیره IP و device
// - خروج از همه دستگاه‌ها (LogoutAll)
// - ذخیره نقش‌ها و مجوزها در AccessToken برای بررسی سریع‌تر
// - فیلتر HasPermission برای بررسی دقیق مجوز