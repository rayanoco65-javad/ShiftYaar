using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.DepartmentModel.DepartmentResponsibilityModel;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.DepartmentModel
{
    public interface IDepartmentResponsibilityService
    {
        Task<ApiResponse<List<DepartmentResponsibilityDtoGet>>> GetByDepartmentIdAsync(int departmentId);
        Task<ApiResponse<DepartmentResponsibilityDtoGet>> GetByIdAsync(int id);
        Task<ApiResponse<DepartmentResponsibilityDtoGet>> CreateAsync(DepartmentResponsibilityDtoAdd dto);
        Task<ApiResponse<DepartmentResponsibilityDtoGet>> UpdateAsync(int id, DepartmentResponsibilityDtoUpdate dto);
        Task<ApiResponse<string>> DeleteAsync(int id);
        Task<ApiResponse<string>> AssignUserResponsibilitiesAsync(UserResponsibilityAssignDto dto);
        Task<ApiResponse<string>> BatchAssignResponsibilitiesAsync(BatchAssignResponsibilitiesDto dto);
        Task<ApiResponse<DepartmentStaffMatrixDto>> GetStaffMatrixAsync(int departmentId);
    }
}
