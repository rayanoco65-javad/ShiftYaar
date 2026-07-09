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
    public interface IDepartmentNameService
    {
        Task<ApiResponse<PagedResponse<DepartmentNameDtoGet>>> GetDepartmentNamesAsync(DepartmentNameFilter filter);
        Task<ApiResponse<DepartmentNameDtoGet>> GetDepartmentAsync(int id);
        Task<ApiResponse<DepartmentNameDtoGet>> CreateDepartmentAsync(DepartmentNameDtoAdd dto);
        Task<ApiResponse<DepartmentNameDtoGet>> UpdateDepartmentAsync(int id, DepartmentNameDtoAdd dto);
        Task<ApiResponse<string>> DeleteDepartmentAsync(int id);
    }
}
