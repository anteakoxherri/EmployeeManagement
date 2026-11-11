
using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.DTOs
{
    // DTO e kerkeses POST /api/auth/login. Nuk ruhet si entitet ne databaze.
    // ApiController kontrollon annotations perpara se kerkesa te arrije te AuthService.
    public class LoginRequest
    {
        // Email-i eshte i detyrueshem dhe duhet te kete format te sakte
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Login kontrollon vetem qe password-i eshte derguar; saktesine e verifikon Identity.
        // Rregullat per password te ri zbatohen gjate krijimit te llogarise, jo ketu.
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
