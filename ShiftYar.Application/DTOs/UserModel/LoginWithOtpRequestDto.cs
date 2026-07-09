using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.UserModel
{
    public class LoginWithOtpRequestDto
    {
        public string PhoneNumberMembership { get; set; }
        public string OtpCode { get; set; }
    }
}
