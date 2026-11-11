using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Server.Controllers
{
    // Pranon kredencialet ne HTTP dhe delegon verifikimin te IAuthService.
    // Nuk krijon perdorues: login-i merr nje llogari ekzistuese dhe kthen JWT per kerkesat e ardhshme.
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        // DI na jep implementimin AuthService pa e ndertuar me new ne controller.
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // Merr LoginRequest nga JSON. AllowAnonymous lejon login pa token, sepse token-i merret pikerisht ketu.
        // Kthen 200 me LoginResponse kur kredencialet jane te sakta ose 401 kur verifikimi deshton.
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);

            if (response == null)
                return Unauthorized("Email ose password i pasakte.");

            return Ok(response);
        }
    }
}
