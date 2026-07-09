using ShiftYar.Application.DTOs.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.Security
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginWithPasswordAsync(LoginWithPasswordRequestDto dto, string ip, string device);
        Task SendOtpAsync_Login(SendOtpRequestDto dto);
        Task<LoginResponseDto> LoginWithOtpAsync(LoginWithOtpRequestDto dto, string ip, string device);
        Task SendOtp_ForgotPasswordAsync(ForgotPasswordRequestDto dto);
        Task ResetPasswordAsync(ResetPasswordRequestDto dto);
        Task<string> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task LogoutAllAsync(string phoneNumberMembership);
    }
}
