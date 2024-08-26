using ProductApi1.Models;
using ProductApi1.Repositories;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace ProductApi1.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(UserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }
        
        public async Task<bool> RegisterUserAsync(RegisterModel model)
        {
            var existingUserByEmail = await _userRepository.GetUserByEmailAsync(model.Email!);
            var existingUserByUsername = await _userRepository.GetUserByUsernameAsync(model.Username!);
            if (existingUserByEmail != null || existingUserByUsername != null)
            {
                return false;
            }

            var user = new User
            {
                Id= Guid.NewGuid().ToString(),
                Username = model.Username!,
                Email = model.Email!,
                PasswordHash = HashPassword(model.Password!)
            };

            await _userRepository.CreateUserAsync(user);
            return true;
        }

        public async Task<string> LoginAsync(LoginModel model)
        {
            var user = await _userRepository.GetUserByUsernameAsync2(model.Username!);

            if (user == null)
            {
                return null!;
            }
            if (!VerifyPassword(model.Password!, user.PasswordHash))
            {
                return null!;
            }
            var token = GenerateJwtToken(user);
            return token;
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            try
            {
                var parts = passwordHash.Split(':');
                var salt = Convert.FromBase64String(parts[0]);
                var hashedPassword = Convert.FromBase64String(parts[1]);

                string hashedInputPassword = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 10000,
                    numBytesRequested: 256 / 8));

                return hashedInputPassword == parts[1];
            }
            catch
            {
                return false;
            }
        }

        public string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id!),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: credentials
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string HashPassword(string password)
        {
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            return $"{Convert.ToBase64String(salt)}:{hashed}";
        }
    }
}