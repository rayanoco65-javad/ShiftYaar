using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.UserModel
{
    public class LoginResponseDto
    {
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public List<string>? Roles { get; set; }
        public User? User { get; set; }
    }
}
