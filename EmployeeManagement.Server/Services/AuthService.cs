
using EmployeeManagement.Models;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EmployeeManagement.Server.Services
{
    // Verifikon llogarite ekzistuese me Identity dhe leshon JWT. AuthController e perdor permes IAuthService.
    // Service merret me autentikimin (kush je); Authorize ne endpoints merret me autorizimin (cfare lejohet).
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        // Marrim UserManager dhe konfigurimet e aplikacionit
        public AuthService(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        // Merr email/password nga LoginRequest. Kthen null kur verifikimi deshton ose LoginResponse me JWT.
        // Password-i nuk perfshihet ne token dhe nuk kthehet ne pergjigje.
        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            // Kerkojme perdoruesin sipas email-it
            var user = await _userManager.FindByEmailAsync(request.Email);

            if (user == null)
                return null;

            // Identity kontrollon password-in kundrejt hash-it te ruajtur, pa e deshifruar hash-in.
            var validPassword = await _userManager.CheckPasswordAsync(
                user, request.Password);

            if (!validPassword)
                return null;

            // Marrim rolet e perdoruesit nga databaza
            var roles = await _userManager.GetRolesAsync(user);

            // Claims jane fushat e token-it: sub identifikon perdoruesin, jti identifikon token-in.
            // JWT eshte i nenshkruar, jo domosdoshmerisht i enkriptuar; permbajtja mund te lexohet nga klienti.
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            // ClaimTypes.Role lejon [Authorize(Roles = "Administrator")] te kontrolloje rolin me vone.
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Marrim konfigurimet e JWT
            var jwtKey = _configuration["Jwt:Key"]!;
            var jwtIssuer = _configuration["Jwt:Issuer"]!;
            var jwtAudience = _configuration["Jwt:Audience"]!;

            // Celesi sekret nenshkruan token-in. API perdor te njejtin celes per te dalluar ndryshimet ne te.
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256);

            // Koha ne UTC shmang ndryshimet e zonave kohore; pas 60 minutash klienti duhet te marre token te ri.
            var expiresAt = DateTime.UtcNow.AddMinutes(60);

            // Gjenerojme JWT token-in
            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler()
                .WriteToken(token);

            // Kthejme token-in dhe te dhenat baze te perdoruesit
            return new LoginResponse
            {
                Token = tokenString,
                ExpiresAt = expiresAt,
                User = new LoginUserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles
                }
            };
        }
    }
}
