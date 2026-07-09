using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Constants;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Interfaces.FileUploaderInterface;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Application.Interfaces.RoleModel;
using ShiftYar.Application.Interfaces.Security;
using ShiftYar.Application.Interfaces.UserModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.UserModel;
using System.ComponentModel;
using System.Security.Claims;

namespace ShiftYar.Application.Features.UserModel.Services
{
    public class UserService : IUserService
    {
        private readonly IEfRepository<User> _repository;
        private readonly IEfRepository<UserPhoneNumber> _repositoryPhoneNumber;
        private readonly IEfRepository<UserRole> _repositoryUserRole;
        private readonly IRoleService _roleService;
        private readonly IAuthService _authService;
        private readonly IMapper _mapper; // AutoMapper
        private readonly ILogger<UserService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IFileUploader _fileUploader;

        public UserService(IEfRepository<User> repository, IEfRepository<UserPhoneNumber> repositoryPhoneNumber,
                                IEfRepository<UserRole> repositoryUserRole, IRoleService roleService, IAuthService authService, IMapper mapper, ILogger<UserService> logger, IHttpContextAccessor httpContextAccessor,
                                IFileUploader fileUploader)
        {
            _repository = repository;
            _mapper = mapper;
            _repositoryPhoneNumber = repositoryPhoneNumber;
            _repositoryUserRole = repositoryUserRole;
            _roleService = roleService;
            _authService = authService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _fileUploader = fileUploader;
        }

        /// Get All User
        public async Task<ApiResponse<PagedResponse<UserDtoGet>>> GetFilteredUsersAsync(UserFilter filter)
        {
            try
            {
                _logger.LogInformation("Fetching users with filter: {@Filter}", filter);
                var result = await _repository.GetByFilterAsync(
                    filter,
                    "OtherPhoneNumbers",
                    "UserRoles",
                    "UserRoles.Role",
                    "Department",
                    "Specialty"
                );
                var data = _mapper.Map<List<UserDtoGet>>(result.Items);

                var pagedResponse = new PagedResponse<UserDtoGet>
                {
                    Items = data,
                    TotalCount = result.TotalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
                };

                _logger.LogInformation("Successfully fetched {Count} users out of {TotalCount}", data.Count, result.TotalCount);
                return ApiResponse<PagedResponse<UserDtoGet>>.Success(pagedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed Fetching users with filter: {@Filter}", filter);
                throw new Exception("سرویس دریافت لیست کاربران با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Get User
        public async Task<ApiResponse<UserDtoAdd>> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Fetching user with ID: {Id}", id);
                var user = await _repository.GetByIdAsync(id, "OtherPhoneNumbers", "UserRoles", "UserRoles.Role", "Department", "Specialty");
                if (user == null)
                {
                    _logger.LogWarning("User with ID {Id} not found", id);
                    return ApiResponse<UserDtoAdd>.Fail("کاربر یافت نشد.");
                }

                var dto = _mapper.Map<UserDtoAdd>(user);
                _logger.LogInformation("Successfully fetched user with ID: {Id}", id);
                return ApiResponse<UserDtoAdd>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed Fetching The user by ID: {Id}", id);
                throw new Exception("سرویس دریافت کاربر موردنظر با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Create User
        public async Task<ApiResponse<UserDtoAdd>> CreateAsync(UserDtoAdd dto)
        {
            try
            {
                _logger.LogInformation("Creating new user with PhoneNumberMembership: {PhoneNumberMembership}", dto.PhoneNumberMembership);
                if (string.IsNullOrEmpty(dto.PhoneNumberMembership))
                {
                    _logger.LogWarning("User creation failed - PhoneNumberMembership Is Empty");
                    return ApiResponse<UserDtoAdd>.Fail("شماره موبایل کاربر نمی تواند خالی باشد.");
                }

                if(dto.PhoneNumberMembership.Length != 11)
                {
                    _logger.LogWarning("User creation failed - PhoneNumberMembership Is Invalid");
                    return ApiResponse<UserDtoAdd>.Fail("شماره موبایل کاربر نامعتبر است.");
                }

                var exists = await _repository.ExistsAsync(u => u.PhoneNumberMembership == dto.PhoneNumberMembership);
                if (exists)
                {
                    _logger.LogWarning("User creation failed - PhoneNumberMembership {PhoneNumberMembership} already exists", dto.PhoneNumberMembership);
                    return ApiResponse<UserDtoAdd>.Fail("کاربری با شماره موبایل وجود دارد.");
                }

                var user = _mapper.Map<User>(dto);
                // تبدیل تاریخ استخدام از شمسی به میلادی
                if (!string.IsNullOrWhiteSpace(dto.DateOfEmployment))
                {
                    user.DateOfEmployment = DateConverter.ConvertToGregorianDate(dto.DateOfEmployment);
                }

                // ست کردن پسورد در صورت وجود
                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                }

                user.Image = _fileUploader.UploadFile(dto.Image, "Users");
                user.CreateDate = DateTime.Now;
                //user.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));
                var sub = _httpContextAccessor.HttpContext?.User?.FindFirst(JwtClaimTypes.Sub)?.Value;
                user.TheUserId = (short)(sub != null && int.TryParse(sub, out var uid) ? uid : 0);

                await _repository.AddAsync(user);
                await _repository.SaveAsync();

                var result = _mapper.Map<UserDtoAdd>(user);
                _logger.LogInformation("Successfully created user with ID: {Id}", user.Id);
                return ApiResponse<UserDtoAdd>.Success(result, "کاربر با موفقیت ایجاد شد.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("User creation failed - NationalCode {NationalCode}", dto.NationalCode);
                throw new Exception("سرویس ایجاد کاربر با خطا مواجه شد : " + ex.Message);
            }
        }


        ///Create User Supervisor And Send Otp For Login
        public async Task<ApiResponse<string>> CreateSupervisorAndSendOtpAsync(UserDtoAdd userDto)
        {
            _logger.LogInformation("Creating new user with PhoneNumberMembership: {PhoneNumberMembership}", userDto.PhoneNumberMembership);
            if (string.IsNullOrEmpty(userDto.PhoneNumberMembership))
            {
                _logger.LogWarning("User creation failed - PhoneNumberMembership Is Empty");
                return ApiResponse<string>.Fail("شماره موبایل کاربر نمی تواند خالی باشد.");
            }

            if (userDto.PhoneNumberMembership.Length != 11)
            {
                _logger.LogWarning("User creation failed - PhoneNumberMembership Is Invalid");
                return ApiResponse<string>.Fail("شماره موبایل کاربر نامعتبر است.");
            }

            var exists = await _repository.ExistsAsync(u => u.PhoneNumberMembership == userDto.PhoneNumberMembership);
            if (exists)
            {
                _logger.LogWarning("User creation failed - PhoneNumberMembership {PhoneNumberMembership} already exists", userDto.PhoneNumberMembership);
                return ApiResponse<string>.Fail("کاربری با این شماره موبایل وجود دارد.");
            }

            //نقش سوپروایزر را پیدا کن
            var roleFilter = new RoleModel.Filters.RoleFilter { Name = "سوپروایزر", PageSize = 1 };
            var roles = await _roleService.GetFilteredRolesAsync(roleFilter);
            var supervisorRole = roles.Data.Items.FirstOrDefault();
            if (supervisorRole == null)
            {
                return ApiResponse<string>.Fail("نقش سوپروایزر یافت نشد.");
            }

            //ست نقش سوپروایزر برای کاربر
            userDto.UserRoles = new List<int> { supervisorRole.Id };

            //ایجاد کاربر
            var createResult = await this.CreateAsync(userDto);
            if (!createResult.IsSuccess)
            {
                return ApiResponse<string>.Fail(createResult.Message ?? "ایجاد کاربر ناموفق بود.");
            }

            //ارسال OTP برای لاگین
            await _authService.SendOtpAsync_Login(new SendOtpRequestDto
            {
                PhoneNumberMembership = userDto.PhoneNumberMembership
            });

            return ApiResponse<string>.Success("کد تایید برای شماره کاربر ارسال شد.");
        }


        ///Update User
        public async Task<ApiResponse<UserDtoAdd>> UpdateAsync(int id, UserDtoAdd dto)
        {
            try
            {
                _logger.LogInformation("Updating user with ID: {Id}", id);

                var user = await _repository.GetByIdAsync(id, "OtherPhoneNumbers", "UserRoles", "UserRoles.Role", "Department", "Specialty");
                if (user == null)
                {
                    _logger.LogWarning("User update failed - User with ID {Id} not found", id);
                    return ApiResponse<UserDtoAdd>.Fail("کاربر یافت نشد.");
                }

                // Handle phone numbers
                if (dto.OtherPhoneNumbers != null)
                {
                    _logger.LogInformation("Updating phone numbers for user {Id}", id);
                    // If the list is empty, remove all phone numbers
                    if (dto.OtherPhoneNumbers.Count == 0)
                    {
                        foreach (var phoneNumber in user.OtherPhoneNumbers.ToList())
                        {
                            _repositoryPhoneNumber.Delete(phoneNumber);
                        }
                    }
                    else
                    {
                        // Remove phone numbers that are not in the new list
                        var phoneNumbersToRemove = user.OtherPhoneNumbers
                            .Where(p => !dto.OtherPhoneNumbers.Contains(p.PhoneNumber))
                            .ToList();

                        foreach (var phoneNumber in phoneNumbersToRemove)
                        {
                            _repositoryPhoneNumber.Delete(phoneNumber);
                        }

                        // Add new phone numbers
                        foreach (var phoneNumber in dto.OtherPhoneNumbers)
                        {
                            if (!user.OtherPhoneNumbers.Any(p => p.PhoneNumber == phoneNumber))
                            {
                                var newPhoneNumber = new UserPhoneNumber
                                {
                                    PhoneNumber = phoneNumber,
                                    IsActive = true,
                                    UserId = user.Id,
                                    User = user // Set the navigation property
                                };

                                newPhoneNumber.CreateDate = DateTime.Now;
                                newPhoneNumber.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

                                user.OtherPhoneNumbers.Add(newPhoneNumber);
                            }
                        }
                    }
                }

                // Handle roles
                if (dto.UserRoles != null)
                {
                    _logger.LogInformation("Updating roles for user {Id}", id);
                    // If the list is empty, remove all user roles
                    if (dto.UserRoles.Count == 0)
                    {
                        foreach (var userRole in user.UserRoles.ToList())
                        {
                            _repositoryUserRole.Delete(userRole);
                        }
                    }
                    else
                    {
                        // Remove user roles that are not in the new list
                        var userRolesToRemove = user.UserRoles
                            .Where(r => !dto.UserRoles.Contains(r.RoleId.Value))
                            .ToList();

                        foreach (var userRole in userRolesToRemove)
                        {
                            _repositoryUserRole.Delete(userRole);
                        }

                        // Add new user roles
                        foreach (var roleId in dto.UserRoles)
                        {
                            if (!user.UserRoles.Any(r => r.RoleId == roleId))
                            {
                                var newUserRole = new UserRole
                                {
                                    RoleId = roleId,
                                    UserId = user.Id,
                                    User = user // Set the navigation property
                                };

                                newUserRole.CreateDate = DateTime.Now;
                                newUserRole.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

                                user.UserRoles.Add(newUserRole);
                            }
                        }
                    }
                }

                // تبدیل تاریخ استخدام از شمسی به میلادی
                if (!string.IsNullOrWhiteSpace(dto.DateOfEmployment))
                {
                    user.DateOfEmployment = DateConverter.ConvertToGregorianDate(dto.DateOfEmployment);
                }

                // Update other user properties
                _mapper.Map(dto, user);

                //Update Image
                if(dto.Image != null)
                {
                    user.Image = _fileUploader.UpdateFile(dto.Image, user.Image, "Users");
                }

                user.CreateDate = DateTime.Now;
                //user.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));
                var sub = _httpContextAccessor.HttpContext?.User?.FindFirst(JwtClaimTypes.Sub)?.Value;
                user.TheUserId = (short)(sub != null && int.TryParse(sub, out var uid) ? uid : 0);

                _repository.Update(user);
                await _repository.SaveAsync();

                var result = _mapper.Map<UserDtoAdd>(user);
                _logger.LogInformation("Successfully updated user with ID: {Id}", id);
                return ApiResponse<UserDtoAdd>.Success(result, "ویرایش کاربر با موفقیت انجام شد.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Failed updated user with ID: {Id}", id);
                throw new Exception("سرویس ویرایش کاربر با خطا مواجه شد : " + ex.Message);
            }
        }


        public async Task<ApiResponse<string>> DeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("Deleting user with ID: {Id}", id);

                var user = await _repository.GetByIdAsync(id, "OtherPhoneNumbers", "UserRoles", "UserRoles.Role");
                if (user == null)
                {
                    _logger.LogWarning("User deletion failed - User with ID {Id} not found", id);
                    return ApiResponse<string>.Fail("کاربر یافت نشد.");
                }

                //Delete all phone numbers of user
                if (user.OtherPhoneNumbers.Count > 0)
                {
                    _logger.LogInformation("Deleting {Count} phone numbers for user {Id}", user.OtherPhoneNumbers.Count, id);
                    foreach (var phoneNumber in user.OtherPhoneNumbers.ToList())
                    {
                        _repositoryPhoneNumber.Delete(phoneNumber);
                    }
                    await _repositoryPhoneNumber.SaveAsync();
                }

                //Delete all user roles
                if (user.UserRoles.Count > 0)
                {
                    _logger.LogInformation("Deleting {Count} roles for user {Id}", user.UserRoles.Count, id);
                    foreach (var userRole in user.UserRoles.ToList())
                    {
                        _repositoryUserRole.Delete(userRole);
                    }
                    await _repositoryUserRole.SaveAsync();
                }

                _repository.Delete(user);
                await _repository.SaveAsync();

                _logger.LogInformation("Successfully deleted user with ID: {Id}", id);
                return ApiResponse<string>.Success("کاربر با موفقیت حذف شد.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Failed Delete user with ID: {Id}", id);
                throw new Exception("سرویس حذف کاربر با خطا مواجه شد : " + ex.Message);
            }
        }
    }
}
