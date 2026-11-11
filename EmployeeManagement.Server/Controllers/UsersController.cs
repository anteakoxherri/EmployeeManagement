using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Server.Controllers
{
    // Pika HTTP per menaxhimin e perdoruesve. Kerkesa kalon nga controller te IUserService,
    // pastaj UserService perdor Identity/EF Core; controller vetem zgjedh pergjigjen HTTP.
    // Authorize ne nivel klase mbron te gjitha metodat: pa token 401, pa rolin Administrator 403.
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        // DI krijon UserService sipas regjistrimit ne Program.cs dhe na e jep permes interfaces.
        // Controller nuk e krijon vete service-in dhe nuk lidhet direkt me SQL Server.
        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // GET /api/users nuk merr parametra dhe kthen 200 me listen e UserDto, perfshire administratoret.
        [HttpGet]
        [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<UserDto>>> GetUsers()
        {
            return Ok(await _userService.GetUsersAsync());
        }

        // POST /api/users merr CreateUserDto dhe perdor te njejtin kufizim Administrator si GET.
        // [ApiController] ndalon kerkesen me 400 nese annotations e DTO-se nuk plotesohen.
        [HttpPost]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto request)
        {
            var result = await _userService.CreateUserAsync(request);

            if (result.DuplicateEmail)
                return Conflict(new { message = "A user with this email already exists." });

            if (result.User == null)
                return BadRequest(new { errors = result.Errors });

            // 201 tregon krijimin; Location tregon URL-ne e GET qe lexon perdoruesin e ri.
            // Kthejme UserDto, kurre CreateUserDto qe permban password-in.
            return CreatedAtAction(nameof(GetUserById), new { id = result.User.Id }, result.User);
        }

        // Merr id nga URL-ja (string sepse Identity perdor string per ID) dhe kthen UserDto ose 404.
        // Task dhe await lejojne pritjen e query ne databaze pa bllokuar thread-in e kerkeses.
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> GetUserById(string id)
        {
            var user = await _userService.GetUserByIdAsync(id);

            if (user == null)
                return NotFound();

            return Ok(user);
        }
    }
}
