using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.Security
{
    public interface IAuthPermissionService
    {
        Task<List<string>> GetPermissionsAsync(int userId);
    }
}
