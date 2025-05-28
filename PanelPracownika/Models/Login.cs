using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PanelPracownika.Models
{
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
    public class Login
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Password { get; set; }

        public string HashPassword(string password)
        {
            var passwordHasher = new PasswordHasher<Login>();
            return passwordHasher.HashPassword(this, password);
        }

        public bool VerifyPassword(string password)
        {
            var passwordHasher = new PasswordHasher<Login>();
            var result = passwordHasher.VerifyHashedPassword(this, Password, password);
            return result == PasswordVerificationResult.Success;
        }

        internal string GenerateToken(string id)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, Username),
            };

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("yourSuperSecretKey@1234567890!@#"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                               issuer: "http://localhost:3000",
                                              audience: "http://localhost:3000",
                                                             claims: claims,
                                                                            expires: DateTime.Now.AddMinutes(30),
                                                                                           signingCredentials: creds
                                                                                                      );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
