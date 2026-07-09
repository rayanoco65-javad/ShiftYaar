using ShiftYar.Application.Interfaces.SmsModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.SmsModel
{
    public class SmsTemplateService : ISmsTemplateService
    {
        private readonly ISmsTemplateRepository _smsTemplateRepository;
        private readonly Dictionary<string, string> _defaultTemplates = new()
        {
            { "login", "کد ورود شما: {code} (اعتبار: {expire} دقیقه)" },
            { "forgotPassword", "کد بازیابی رمز عبور: {code} (اعتبار: {expire} دقیقه)" },
            // سایر قالب‌ها...
        };

        public SmsTemplateService(ISmsTemplateRepository smsTemplateRepository)
        {
            _smsTemplateRepository = smsTemplateRepository;
        }

        public async Task<string> GetTemplateAsync(string templateKey, params (string key, string value)[] parameters)
        {
            var dbTemplate = await _smsTemplateRepository.GetActiveTemplateByKeyAsync(templateKey);
            var template = dbTemplate?.TemplateText ?? (_defaultTemplates.TryGetValue(templateKey, out var def) ? def : "{code}");
            foreach (var (key, value) in parameters)
            {
                template = Regex.Replace(template, "\\{" + key + "\\}", value);
            }
            return template;
        }

        public string GetTemplate(string templateKey, params (string key, string value)[] parameters)
        {
            return GetTemplateAsync(templateKey, parameters).GetAwaiter().GetResult();
        }
    }
}
