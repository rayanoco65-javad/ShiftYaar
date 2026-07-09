using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.RoleModel;
using ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel;
using ShiftYar.Application.Features.RoleModel.Filters;
using ShiftYar.Application.Features.SpecialtyModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.SpecialtyModel
{
    public interface ISpecialtyService
    {
        Task<ApiResponse<PagedResponse<SpecialtyDtoGet>>> GetSpecialties(SpecialtyFilter filter);
        Task<ApiResponse<SpecialtyDtoGet>> GetSpecialty(int id);
        Task<ApiResponse<SpecialtyDtoGet>> CreateSpecialty(SpecialtyDtoAdd dto);
        Task<ApiResponse<SpecialtyDtoGet>> UpdateSpecialty(int id, SpecialtyDtoAdd dto);
        Task<ApiResponse<string>> DeleteSpecialty(int id);
    }
}
