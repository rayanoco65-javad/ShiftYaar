using Microsoft.EntityFrameworkCore;
using ShiftYar.Application.Interfaces.SmsModel;
using ShiftYar.Domain.Entities.SmsModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Infrastructure.Persistence.Repositories
{
    public class SmsTemplateRepository : ISmsTemplateRepository
    {
        private readonly ShiftYarDbContext _context;

        public SmsTemplateRepository(ShiftYarDbContext context)
        {
            _context = context;
        }

        public async Task<SmsTemplate?> GetActiveTemplateByKeyAsync(string templateKey)
        {
            return await _context.SmsTemplates
                .FirstOrDefaultAsync(t => t.TemplateKey == templateKey && t.IsActive);
        }
    }
}
