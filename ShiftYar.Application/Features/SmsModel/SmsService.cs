using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShiftYar.Application.Interfaces.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.SmsModel
{
    public class SmsService : ISmsService
    {
        private readonly ILogger<SmsService> _logger;
        private readonly IConfiguration _configuration;

        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public SmsService(ILogger<SmsService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _apiKey = configuration["KavenegarConfig:Key"];
            _httpClient = new HttpClient();
        }

        public async Task SendSmsAsync(string phoneNumber, string message)
        {
            _logger.LogInformation($"ارسال پیامک به {phoneNumber}: {message}");

            try
            {
                var sender = _configuration["KavenegarConfig:Sender"];
                var receptor = phoneNumber;
                Kavenegar.KavenegarApi api = new Kavenegar.KavenegarApi(_configuration["KavenegarConfig:Key"]);
                await api.Send(sender, receptor, message);
            }
            catch (Kavenegar.Core.Exceptions.ApiException ex)
            {
                throw new Exception("اجرای وب سرویس ارسال پیامک با خطا مواجه شد : " + ex.Message);
            }
            catch (Kavenegar.Core.Exceptions.HttpException ex)
            {
                throw new Exception("ارتباط با وب سرویس برقرار نشد : " + ex.Message);
            }

            await Task.CompletedTask;
        }

        //وقتی شماره اختصاصی داشته باشیم میتوانیم از متد Send وب سرویس کاوه نگار استفاده کنیم . الگوهای پیامک خودمان را ارسال کنیم
        //اما اگر شماره اختصاصی نداشته باشیم برای ارسال otp بایستی الگوها را در پنل کاوه نگار نعریف کرده و برای ارسال پیامک از Lookup استفاده کنیم.


        //برای ارسال otp براساس الگوهای تعریف شده در پنل کاوه نگار
        //هر الگو در پنل یک نام دارد و درون هر الگو یک تا سه متغیر میتوانیم داشته باشیم
        //receptor : شماره گیرنده      template: نام الگوی تعریف شده در پنل      token: مقدار متغیر اصلی درون متن پیامک که اینجا کد اتی پی است    token2,3: مقدار سایر متغیرها درصورت وجود در الگو
        //پارامترهای receptor و template و token اجباری هستند.
        public async Task<bool> SendLookupAsync(string receptor, string token, string template, string token2 = null, string token3 = null)
        {
            try
            {
                _logger.LogInformation($"ارسال پیامک به {receptor}: {template}");

                var url = $"https://api.kavenegar.com/v1/{_apiKey}/verify/lookup.json" +
                          $"?receptor={receptor}&token={token}&template={template}";

                if (!string.IsNullOrEmpty(token2))
                    url += $"&token2={token2}";
                if (!string.IsNullOrEmpty(token3))
                    url += $"&token3={token3}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return false;

                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Kavenegar Response: " + content); // برای تست

                _logger.LogInformation($"ارسال پیامک به {receptor}: {content}");

                return true;
            }
            catch (Kavenegar.Core.Exceptions.ApiException ex)
            {
                throw new Exception("اجرای وب سرویس ارسال پیامک با خطا مواجه شد : " + ex.Message);
            }
            catch (Kavenegar.Core.Exceptions.HttpException ex)
            {
                throw new Exception("ارتباط با وب سرویس برقرار نشد : " + ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception("عملیات ارسال پیامک با خطا مواجه شد : " + ex.Message);
            }
        }

    }
}
