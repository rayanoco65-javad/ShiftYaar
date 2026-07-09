using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.SmsModel
{
    public interface ISmsTemplateService
    {
        string GetTemplate(string templateKey, params (string key, string value)[] parameters);
        Task<string> GetTemplateAsync(string templateKey, params (string key, string value)[] parameters);
    }
}
