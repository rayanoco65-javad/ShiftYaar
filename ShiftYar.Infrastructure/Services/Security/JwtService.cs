using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
//using ShiftYar.Application.Common.Constants;
using ShiftYar.Application.Interfaces.Security;
using ShiftYar.Domain.Entities.SecurityModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Infrastructure.Services.Security
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateAccessToken(User user, List<string> roles, List<string> permissions)
        {
            var claims = new List<Claim>
            {
                ////new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                ////new Claim(ClaimTypes.Name, user.FullName),
                //new Claim(JwtClaimNames.Sub, user.Id.ToString()),
                //new Claim(JwtClaimNames.Name, user.FullName ?? string.Empty),
                new Claim("Sub", user.Id.ToString()),
                new Claim("Name", user.FullName ?? string.Empty),
            };

            foreach (var role in roles)
            {
                ////claims.Add(new Claim(ClaimTypes.Role, role));
                //claims.Add(new Claim(JwtClaimTypes.Role, role));
                claims.Add(new Claim("Role", role));
            }

            foreach (var permission in permissions)
            {
                ////claims.Add(new Claim("permission", permission));
                //claims.Add(new Claim(JwtClaimTypes.Permission, permission));
                claims.Add(new Claim("Permission", permission));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtConfig:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["JwtConfig:Issuer"],
                audience: _config["JwtConfig:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_config["JwtConfig:AccessTokenExpirationMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        //public string GenerateRefreshToken()
        //{
        //    var randomBytes = new byte[64];
        //    using var rng = RandomNumberGenerator.Create();
        //    rng.GetBytes(randomBytes);
        //    return Convert.ToBase64String(randomBytes);
        //}

        public RefreshToken GenerateRefreshToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomBytes),
                Expires = DateTime.Now.AddDays(Convert.ToDouble(_config["JwtConfig:RefreshTokenExpirationDays"])),
                IsRevoked = false,
                CreateDate = DateTime.Now
            };
        }
    }
}
