using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Interfaces.Security;
using ShiftYar.Application.Interfaces.SmsModel;
using ShiftYar.Domain.Entities.SecurityModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Infrastructure.Services.Security
{
    public class AuthService : IAuthService
    {
        private readonly ShiftYarDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IJwtService _jwtService;
        private readonly ISmsTemplateService _smsTemplateService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(ShiftYarDbContext context, ISmsService smsService, IJwtService jwtService, ISmsTemplateService smsTemplateService, ILogger<AuthService> logger)
        {
            _context = context;
            _smsService = smsService;
            _jwtService = jwtService;
            _smsTemplateService = smsTemplateService;
            _logger = logger;
        }

        public async Task<LoginResponseDto> LoginWithPasswordAsync(LoginWithPasswordRequestDto dto, string ip, string device)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .Include(u => u.RefreshTokens)
                .Include(u => u.LoginHistories)
                .SingleOrDefaultAsync(x => x.PhoneNumberMembership == dto.PhoneNumberMembership);

            if (user.Password == null)
            {
                _logger.LogWarning($"ورود ناموفق | شماره تلفن: {dto.PhoneNumberMembership} | IP: {ip} | دستگاه: {device}");
                throw new UnauthorizedAccessException("نام کاربری یا رمز عبور اشتباه است.");
            }

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
            {
                _logger.LogWarning($"ورود ناموفق | شماره تلفن: {dto.PhoneNumberMembership} | IP: {ip} | دستگاه: {device}");
                throw new UnauthorizedAccessException("نام کاربری یا رمز عبور اشتباه است.");
            }

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToList();

            var accessToken = _jwtService.GenerateAccessToken(user, roles, permissions);
            var refreshToken = _jwtService.GenerateRefreshToken();

            //Add RefToKen To DB
            user.RefreshTokens.Add(refreshToken);

            //Add Login History To DB
            user.LoginHistories.Add(new LoginHistory
            {
                LoginTime = DateTime.Now,
                IPAddress = ip,
                Device = device
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation($"ورود موفق | کاربر: {user.FullName} (ID: {user.Id}) | نقش‌ها: {string.Join(", ", roles)} | IP: {ip} | دستگاه: {device}");

            return new LoginResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshToken.Token,
                Roles = roles,
                User = user
            };
        }

        public async Task SendOtpAsync_Login(SendOtpRequestDto dto)
        {
            const int expireMinutes = 5;
            const int maxAttempts = 5;
            var now = DateTime.Now;
            var phone = dto.PhoneNumberMembership;

            //آیا این شماره قبلا ثبت مام کرده که بخواد وارد بشه
            var userExist = _context.Users.Any(u => u.PhoneNumberMembership == dto.PhoneNumberMembership);
            if (!userExist)
                throw new Exception("شماره وارد شده هنوز ثبت نام نکرده است. لطفا ابتدا ثبت نام کنید.");

            // حذف کدهای منقضی شده
            var expiredOtps = _context.OtpCodes.Where(o => o.PhoneNumber == phone && o.ExpireAt < now);
            _context.OtpCodes.RemoveRange(expiredOtps);
            await _context.SaveChangesAsync();

            // شمارش تلاش‌های فعال
            var activeOtps = _context.OtpCodes.Where(o => o.PhoneNumber == phone && !o.IsUsed && o.ExpireAt > now);
            if (await activeOtps.CountAsync() >= maxAttempts)
                throw new InvalidOperationException($"تعداد تلاش‌های مجاز برای ارسال کد به پایان رسیده است. لطفا بعدا تلاش کنید.");

            // تولید کد تصادفی ۵ رقمی
            var code = new Random().Next(10000, 99999).ToString();
            var expireAt = now.AddMinutes(expireMinutes);

            // ذخیره در دیتابیس
            var otp = new OtpCode
            {
                PhoneNumber = phone,
                Code = code,
                ExpireAt = expireAt,
                IsUsed = false
            };
            _context.OtpCodes.Add(otp);
            await _context.SaveChangesAsync();

            //// ارسال پیامک با قالب داینامیک
            //string message = _smsTemplateService.GetTemplate("login", ("code", code), ("expire", expireMinutes.ToString()));
            //await _smsService.SendSmsAsync(phone, message);

            //ارسال پیامک با قالب تعریف شده در پنل کاوه نگار
            //login : اسم قالب پیامکی است که در پنل ایده نگار تعریف کردم
            await _smsService.SendLookupAsync(phone, code, "login", expireMinutes.ToString());
        }

        public async Task<LoginResponseDto> LoginWithOtpAsync(LoginWithOtpRequestDto dto, string ip, string device)
        {
            var now = DateTime.Now;
            var phone = dto.PhoneNumberMembership;
            var code = dto.OtpCode;

            // پیدا کردن OTP معتبر و مصرف‌نشده
            var otp = await _context.OtpCodes
                .Where(o => o.PhoneNumber == phone && o.Code == code && !o.IsUsed && o.ExpireAt > now)
                .OrderByDescending(o => o.ExpireAt)
                .FirstOrDefaultAsync();

            if (otp == null)
                throw new UnauthorizedAccessException("کد وارد شده معتبر نیست یا منقضی شده است.");

            // پیدا کردن کاربر
            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .Include(u => u.RefreshTokens)
                .Include(u => u.LoginHistories)
                .SingleOrDefaultAsync(x => x.PhoneNumberMembership == phone);

            if (user == null)
                throw new UnauthorizedAccessException("کاربری با این شماره یافت نشد.");

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToList();

            var accessToken = _jwtService.GenerateAccessToken(user, roles, permissions);
            var refreshToken = _jwtService.GenerateRefreshToken();

            user.RefreshTokens.Add(refreshToken);

            user.LoginHistories.Add(new LoginHistory
            {
                LoginTime = DateTime.UtcNow,
                IPAddress = ip,
                Device = device
            });

            // ابطال کد مصرف‌شده
            otp.IsUsed = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"ورود موفق با OTP | کاربر: {user.FullName} (ID: {user.Id}) | نقش‌ها: {string.Join(", ", roles)} | IP: {ip} | دستگاه: {device}");

            return new LoginResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshToken.Token,
                Roles = roles,
                User = user
            };
        }

        public async Task SendOtp_ForgotPasswordAsync(ForgotPasswordRequestDto dto)
        {
            const int expireMinutes = 5;
            const int maxAttempts = 5;
            var now = DateTime.Now;
            var phone = dto.PhoneNumberMembership;

            //آیا این شماره قبلا ثبت نام کرده که بخواد رمزش رو عوض کنه
            var userExist = _context.Users.Any(u => u.PhoneNumberMembership == dto.PhoneNumberMembership);
            if (!userExist)
                throw new Exception("شماره وارد شده هنوز ثبت نام نکرده است. لطفا ابتدا ثبت نام کنید.");

            // حذف کدهای منقضی شده
            var expiredCodes = _context.PasswordResetCodes.Where(o => o.PhoneNumber == phone && o.ExpireAt < now);
            _context.PasswordResetCodes.RemoveRange(expiredCodes);
            await _context.SaveChangesAsync();

            // شمارش تلاش‌های فعال
            var activeCodes = _context.PasswordResetCodes.Where(o => o.PhoneNumber == phone && !o.IsUsed && o.ExpireAt > now);
            if (await activeCodes.CountAsync() >= maxAttempts)
                throw new InvalidOperationException($"تعداد تلاش‌های مجاز برای ارسال کد بازیابی به پایان رسیده است. لطفا بعدا تلاش کنید.");

            // تولید کد تصادفی ۵ رقمی
            var code = new Random().Next(10000, 99999).ToString();
            var expireAt = now.AddMinutes(expireMinutes);

            // ذخیره در دیتابیس
            var resetCode = new PasswordResetCode
            {
                PhoneNumber = phone,
                Code = code,
                ExpireAt = expireAt,
                IsUsed = false
            };
            _context.PasswordResetCodes.Add(resetCode);
            await _context.SaveChangesAsync();

            //// ارسال پیامک با قالب داینامیک
            //string message = _smsTemplateService.GetTemplate("forgotPassword", ("code", code), ("expire", expireMinutes.ToString()));
            //await _smsService.SendSmsAsync(phone, message);

            //ارسال پیامک با قالب تعریف شده در پنل کاوه نگار
            //forgotPassword : اسم قالب پیامکی است که در پنل ایده نگار تعریف کردم
            await _smsService.SendLookupAsync(phone, code, "forgotPassword", expireMinutes.ToString());
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto dto)
        {
            var now = DateTime.Now;
            var phone = dto.PhoneNumberMembership;
            var code = dto.OtpCode;
            var newPassword = dto.NewPassword;

            // پیدا کردن کد بازیابی معتبر و مصرف‌نشده
            var resetCode = await _context.PasswordResetCodes
                .Where(o => o.PhoneNumber == phone && o.Code == code && !o.IsUsed && o.ExpireAt > now)
                .OrderByDescending(o => o.ExpireAt)
                .FirstOrDefaultAsync();

            if (resetCode == null)
                throw new UnauthorizedAccessException("کد بازیابی معتبر نیست یا منقضی شده است.");

            // پیدا کردن کاربر
            var user = await _context.Users.SingleOrDefaultAsync(x => x.PhoneNumberMembership == phone);
            if (user == null)
                throw new UnauthorizedAccessException("کاربری با این شماره یافت نشد.");

            // تغییر رمز با BCrypt
            user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            resetCode.IsUsed = true;
            await _context.SaveChangesAsync();
        }

        public async Task<string> RefreshTokenAsync(string refreshToken)
        {
            var user = await _context.Users
                .Include(u => u.RefreshTokens)
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .SingleOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken && !(bool)rt.IsRevoked && rt.Expires > DateTime.UtcNow));

            if (user == null)
                throw new UnauthorizedAccessException("RefreshToken نامعتبر است یا منقضی شده است");

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToList();

            var newAccessToken = _jwtService.GenerateAccessToken(user, roles, permissions);
            return newAccessToken;
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var user = await _context.Users
                .Include(u => u.RefreshTokens)
                .SingleOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken));

            if (user != null)
            {
                var token = user.RefreshTokens.FirstOrDefault(rt => rt.Token == refreshToken);
                if (token != null)
                {
                    token.IsRevoked = true;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                throw new UnauthorizedAccessException("کاربر یافت نشد.");
            }
        }

        public async Task LogoutAllAsync(string phoneNumberMembership)
        {
            var user = _context.Users
                .Include(u => u.RefreshTokens)
                .SingleOrDefault(x => x.PhoneNumberMembership == phoneNumberMembership);

            if (user == null)
                throw new UnauthorizedAccessException("کاربر یافت نشد.");

            foreach (var token in user.RefreshTokens)
            {
                token.IsRevoked = true;
            }
            await _context.SaveChangesAsync();
        }
    }
}
