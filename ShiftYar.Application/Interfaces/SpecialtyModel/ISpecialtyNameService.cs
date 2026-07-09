using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using ShiftYar.Application.Features.SpecialtyModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.SpecialtyModel
{
    public interface ISpecialtyNameService
    {
        Task<ApiResponse<PagedResponse<SpecialtyNameDtoGet>>> GetSpecialtyNamesAsync(SpecialtyNameFilter filter);
        Task<ApiResponse<SpecialtyNameDtoGet>> GetSpecialtyNameAsync(int id);
        Task<ApiResponse<SpecialtyNameDtoGet>> CreateSpecialtyNameAsync(SpecialtyNameDtoAdd dto);
        Task<ApiResponse<SpecialtyNameDtoGet>> UpdateSpecialtyNameAsync(int id, SpecialtyNameDtoAdd dto);
        Task<ApiResponse<string>> DeleteSpecialtyNameAsync(int id);
    }
}
