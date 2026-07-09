using ShiftYar.Application.DTOs.ShiftExchangeModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces
{
    public interface IShiftExchangeService
    {
        Task<ShiftExchangeDtoGet?> GetByIdAsync(int id);
        Task<List<ShiftExchangeDtoGet>> GetAllAsync();
        Task<List<ShiftExchangeDtoGet>> GetByUserIdAsync(int userId);
        Task<List<ShiftExchangeDtoGet>> GetPendingApprovalsAsync(int supervisorId);
        Task<ShiftExchangeDtoGet?> CreateAsync(ShiftExchangeDtoAdd dto);
        Task<ShiftExchangeDtoGet?> UpdateAsync(ShiftExchangeDtoUpdate dto);
        Task<bool> ApproveAsync(ShiftExchangeApprovalDto dto);
        Task<bool> ExecuteExchangeAsync(int exchangeId);
        Task<bool> CancelAsync(int exchangeId);
        Task<bool> DeleteAsync(int id);
    }
}
