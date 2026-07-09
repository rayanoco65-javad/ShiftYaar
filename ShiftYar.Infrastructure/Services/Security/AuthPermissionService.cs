using Microsoft.EntityFrameworkCore;
using ShiftYar.Application.Interfaces.Security;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Infrastructure.Services.Security
{
    public class AuthPermissionService : IAuthPermissionService
    {
        private readonly ShiftYarDbContext _context;

        public AuthPermissionService(ShiftYarDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetPermissionsAsync(int userId)
        {
            return await _context.Users
                .Where(u => u.Id == userId)
                .SelectMany(u => u.UserRoles)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToListAsync();
        }
    }

}
