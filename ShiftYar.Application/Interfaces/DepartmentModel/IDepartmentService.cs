using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel;
using ShiftYar.Application.Features.DepartmentModel.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.DepartmentModel
{
    public interface IDepartmentService
    {
        Task<ApiResponse<PagedResponse<DepartmentDtoGet>>> GetFilteredDepartmentsAsync(DepartmentFilter filter);
        Task<ApiResponse<DepartmentDtoGet>> GetByIdAsync(int id);
        Task<ApiResponse<DepartmentDtoGet>> CreateAsync(DepartmentDtoAdd dto);
        Task<ApiResponse<DepartmentDtoGet>> UpdateAsync(int id, DepartmentDtoAdd dto);
        Task<ApiResponse<string>> DeleteAsync(int id);
    }
}