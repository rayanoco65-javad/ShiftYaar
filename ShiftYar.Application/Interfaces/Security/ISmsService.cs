using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.Security
{
    public interface ISmsService
    {
        Task SendSmsAsync(string phoneNumber, string message);
        Task<bool> SendLookupAsync(string receptor, string token, string template, string token2 = null, string token3 = null);
    }
}
