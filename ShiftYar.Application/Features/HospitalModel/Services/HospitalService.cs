using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.HospitalModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.HospitalModel.Filters;
using ShiftYar.Application.Interfaces.FileUploaderInterface;
using ShiftYar.Application.Interfaces.HospitalModel;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.UserModel;
using System.Security.Claims;

namespace ShiftYar.Infrastructure.Persistence.Repositories.HospitalModel
{
    public class HospitalService : IHospitalService
    {
        private readonly IEfRepository<Hospital> _repository;
        private readonly IEfRepository<HospitalPhoneNumber> _repositoryPhoneNumber;
        private readonly IMapper _mapper; // AutoMapper
        private readonly ILogger<HospitalService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IFileUploader _fileUploader;

        public HospitalService(IEfRepository<Hospital> repository, IEfRepository<HospitalPhoneNumber> repositoryPhoneNumber,
                                IMapper mapper, ILogger<HospitalService> logger, IHttpContextAccessor httpContextAccessor, IFileUploader fileUploader)
        {
            _repository = repository;
            _repositoryPhoneNumber = repositoryPhoneNumber;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _fileUploader = fileUploader;
        }

        ///Get All Hospital
        public async Task<ApiResponse<PagedResponse<HospitalDtoGet>>> GetFilteredUsersAsync(HospitalFilter filter)
        {
            _logger.LogInformation("Fetching hospitals with filter: {@Filter}", filter);

            var result = await _repository.GetByFilterAsync(filter, "PhoneNumbers");
            var data = _mapper.Map<List<HospitalDtoGet>>(result.Items);

            var pagedResponse = new PagedResponse<HospitalDtoGet>
            {
                Items = data,
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize)
            };

            _logger.LogInformation("Successfully fetched {Count} hospitals out of {TotalCount}", data.Count, result.TotalCount);

            return ApiResponse<PagedResponse<HospitalDtoGet>>.Success(pagedResponse);
        }

        ///Get Hospital
        public async Task<ApiResponse<HospitalDtoGet>> GetByIdAsync(int id)
        {
            _logger.LogInformation("Fetching hospital with ID: {Id}", id);

            var hospital = await _repository.GetByIdAsync(id, "PhoneNumbers");
            if (hospital == null)
            {
                _logger.LogWarning("Hospital with ID {Id} not found", id);
                return ApiResponse<HospitalDtoGet>.Fail("بیمارستان موردنظر یافت نشد.");
            }

            var dto = _mapper.Map<HospitalDtoGet>(hospital);
            _logger.LogInformation("Successfully fetched hospital with ID: {Id}", id);
            return ApiResponse<HospitalDtoGet>.Success(dto);
        }

        ///Create Hospital
        public async Task<ApiResponse<HospitalDtoAdd>> CreateAsync(HospitalDtoAdd dto)
        {
            _logger.LogInformation("Creating new hospital with SiamCode: {SiamCode}", dto.SiamCode);

            if (!string.IsNullOrEmpty(dto.SiamCode))
            {
                var exists = await _repository.ExistsAsync(u => u.SiamCode == dto.SiamCode);
                if (exists)
                {
                    _logger.LogWarning("Hospital creation failed - SiamCode {SiamCode} already exists", dto.SiamCode);
                    return ApiResponse<HospitalDtoAdd>.Fail("بیمارستانی با این کد سیام وجد دارد.");
                }
            }

            var hospital = _mapper.Map<Hospital>(dto);

            hospital.Logo = _fileUploader.UploadFile(dto.Logo, "Hospitals");
            hospital.CreateDate = DateTime.Now;
            hospital.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            await _repository.AddAsync(hospital);
            await _repository.SaveAsync();

            var result = _mapper.Map<HospitalDtoAdd>(hospital);
            _logger.LogInformation("Successfully created hospital with ID: {Id}", hospital.Id);
            return ApiResponse<HospitalDtoAdd>.Success(result, "بیمارستان با موفقیت ایجاد شد.");
        }

        ///Update User
        public async Task<ApiResponse<HospitalDtoAdd>> UpdateAsync(int id, HospitalDtoAdd dto)
        {
            _logger.LogInformation("Updating hospital with ID: {Id}", id);

            var hospital = await _repository.GetByIdAsync(id, "PhoneNumbers");
            if (hospital == null)
            {
                _logger.LogWarning("Hospital update failed - Hospital with ID {Id} not found", id);
                return ApiResponse<HospitalDtoAdd>.Fail("بیمارستان موردنظر یافت نشد.");
            }

            // Handle phone numbers
            if (dto.PhoneNumbers != null)
            {
                _logger.LogInformation("Updating phone numbers for hospital {Id}", id);
                // If the list is empty, remove all phone numbers
                if (dto.PhoneNumbers.Count == 0)
                {
                    foreach (var phoneNumber in hospital.PhoneNumbers.ToList())
                    {
                        _repositoryPhoneNumber.Delete(phoneNumber);
                    }
                }
                else
                {
                    // Remove phone numbers that are not in the new list
                    var phoneNumbersToRemove = hospital.PhoneNumbers
                        .Where(p => !dto.PhoneNumbers.Contains(p.PhoneNumber))
                        .ToList();

                    foreach (var phoneNumber in phoneNumbersToRemove)
                    {
                        _repositoryPhoneNumber.Delete(phoneNumber);
                    }

                    // Add new phone numbers
                    foreach (var phoneNumber in dto.PhoneNumbers)
                    {
                        if (!hospital.PhoneNumbers.Any(p => p.PhoneNumber == phoneNumber))
                        {
                            var newPhoneNumber = new HospitalPhoneNumber
                            {
                                PhoneNumber = phoneNumber,
                                IsActive = true,
                                HospitalId = hospital.Id,
                                Hospital = hospital // Set the navigation property
                            };
                            hospital.PhoneNumbers.Add(newPhoneNumber);
                        }
                    }
                }
            }

            // Update other hospital properties
            _mapper.Map(dto, hospital);

            if(dto.Logo != null)
                hospital.Logo = _fileUploader.UpdateFile(dto.Logo, hospital.Logo, "Hospitals");

            hospital.UpdateDate = DateTime.Now;
            hospital.TheUserId = Convert.ToInt16(_httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier));

            _repository.Update(hospital);
            await _repository.SaveAsync();

            var result = _mapper.Map<HospitalDtoAdd>(hospital);
            _logger.LogInformation("Successfully updated Hospital with ID: {Id}", id);
            return ApiResponse<HospitalDtoAdd>.Success(result, "ویرایش بیمارستان با موفقیت انجام شد.");
        }

        ///Delete Hospital
        public async Task<ApiResponse<string>> DeleteAsync(int id)
        {
            _logger.LogInformation("Deleting hospital with ID: {Id}", id);

            var hospital = await _repository.GetByIdAsync(id, "PhoneNumbers");
            if (hospital == null)
            {
                _logger.LogWarning("Hospital deletion failed - Hospital with ID {Id} not found", id);
                return ApiResponse<string>.Fail("بیمارستان موردنظر یافت نشد.");
            }

            //Delete all phone numbers of hospital
            if (hospital.PhoneNumbers.Count > 0)
            {
                _logger.LogInformation("Deleting {Count} phone numbers for hospital {Id}", hospital.PhoneNumbers.Count, id);
                foreach (var phoneNumber in hospital.PhoneNumbers.ToList())
                {
                    _repositoryPhoneNumber.Delete(phoneNumber);
                }
                await _repositoryPhoneNumber.SaveAsync();
            }

            _repository.Delete(hospital);
            await _repository.SaveAsync();

            _logger.LogInformation("Successfully deleted hospital with ID: {Id}", id);
            return ApiResponse<string>.Success("بیمارستان موردنظر با موفقیت حذف شد.");
        }
    }
}
