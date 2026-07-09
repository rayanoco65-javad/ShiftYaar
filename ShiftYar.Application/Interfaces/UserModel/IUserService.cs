using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.UserModel
{
    public interface IUserService
    {
        Task<ApiResponse<PagedResponse<UserDtoGet>>> GetFilteredUsersAsync(UserFilter filter);
        Task<ApiResponse<UserDtoAdd>> GetByIdAsync(int id);
        Task<ApiResponse<UserDtoAdd>> CreateAsync(UserDtoAdd dto);
        Task<ApiResponse<string>> CreateSupervisorAndSendOtpAsync(UserDtoAdd userDto);
        Task<ApiResponse<UserDtoAdd>> UpdateAsync(int id, UserDtoAdd dto);
        Task<ApiResponse<string>> DeleteAsync(int id);
    }
}
