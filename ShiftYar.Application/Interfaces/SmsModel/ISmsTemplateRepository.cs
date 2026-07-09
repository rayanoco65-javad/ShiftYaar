using ShiftYar.Domain.Entities.SmsModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.SmsModel
{
    public interface ISmsTemplateRepository
    {
        Task<SmsTemplate?> GetActiveTemplateByKeyAsync(string templateKey);
    }
}
