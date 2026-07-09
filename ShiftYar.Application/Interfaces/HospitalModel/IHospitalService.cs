using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.HospitalModel;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.HospitalModel.Filters;
using ShiftYar.Application.Features.UserModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.HospitalModel
{
    public interface IHospitalService
    {
        //Task<IEnumerable<HospitalDtoAdd>> GetAllHospitalAsync();
        //Task AddHospitalAsync(HospitalDtoAdd hospitalDto);

        Task<ApiResponse<PagedResponse<HospitalDtoGet>>> GetFilteredUsersAsync(HospitalFilter filter);
        Task<ApiResponse<HospitalDtoGet>> GetByIdAsync(int id);
        Task<ApiResponse<HospitalDtoAdd>> CreateAsync(HospitalDtoAdd dto);
        Task<ApiResponse<HospitalDtoAdd>> UpdateAsync(int id, HospitalDtoAdd dto);
        Task<ApiResponse<string>> DeleteAsync(int id);

    }
}
