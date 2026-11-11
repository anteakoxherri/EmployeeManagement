using EmployeeManagement.Models;
using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Server.Services
{
    // Zbaton kontraten IUserService dhe mban logjiken e perdoruesve jashte controller-it.
    // UserManager ofron rregullat e Identity; DbContext mundeson query EF Core dhe transaksionet.
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        // DI jep te njejtin DbContext te scope-it edhe te Identity, qe shkrimet te ndajne transaksionin.
        public UserService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // Pa parametra; ToListAsync ekzekuton SQL-ne dhe kthen te gjithe perdoruesit si DTO te sigurta.
        public Task<List<UserDto>> GetUsersAsync()
        {
            return QueryUsers().ToListAsync();
        }

        // Merr ID-ne e perdoruesit dhe kthen DTO ose null; controller e shnderron null ne HTTP 404.
        public Task<UserDto?> GetUserByIdAsync(string id)
        {
            return QueryUsers().SingleOrDefaultAsync(user => user.Id == id);
        }

        // Merr DTO-ne e administratorit dhe krijon perdoruesin me rolin e zgjedhur.
        // UserManager kontrollon password-in dhe ruan hash-in; service nuk ruan password te lexueshem.
        public async Task<CreateUserResult> CreateUserAsync(CreateUserDto request)
        {
            if (request.Role != "Employee" && request.Role != "Administrator")
                return new CreateUserResult { Errors = new[] { "Role must be Employee or Administrator." } };

            var email = request.Email.Trim();

            // Identity normalizon email-in, prandaj ndryshimi i shkronjave nuk shmang kontrollin.
            if (await _userManager.FindByEmailAsync(email) != null)
                return new CreateUserResult { DuplicateEmail = true };

            // Rolet krijohen nga DbSeeder. Ky endpoint cakton vetem nje rol ekzistues.
            if (!await _context.Roles.AnyAsync(role => role.NormalizedName == request.Role.ToUpperInvariant()))
                return new CreateUserResult { Errors = new[] { "The selected role does not exist." } };

            // Transaksioni i ruan perdoruesin dhe rolin si nje veprim te vetem.
            // Nese kthehemi pa CommitAsync, DisposeAsync anulon shkrimet dhe nuk mbetet perdorues pa rol.
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = request.FirstName,
                    LastName = request.LastName
                };

                var creation = await _userManager.CreateAsync(user, request.Password);

                if (!creation.Succeeded)
                {
                    // Nje kerkese tjeter mund ta kete krijuar te njejtin email nderkohe.
                    return new CreateUserResult
                    {
                        DuplicateEmail = creation.Errors.Any(error =>
                            error.Code == "DuplicateEmail" || error.Code == "DuplicateUserName"),
                        Errors = creation.Errors.Select(error => error.Description).ToArray()
                    };
                }

                var assignment = await _userManager.AddToRoleAsync(user, request.Role);

                if (!assignment.Succeeded)
                    return new CreateUserResult { Errors = assignment.Errors.Select(error => error.Description).ToArray() };

                // Riperdorim leximin ekzistues qe pergjigjja te permbaje vetem fushat e UserDto.
                var createdUser = await GetUserByIdAsync(user.Id);
                await transaction.CommitAsync();

                return new CreateUserResult { User = createdUser };
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // SQL Server mbron unikatesine edhe kur dy kerkesa vijne njekohesisht.
                // UserName eshte email-i; indeksi unik i Identity ndalon dy regjistrime me te njejtin email.
                return new CreateUserResult { DuplicateEmail = true };
            }
        }

        // Nderton nje query te riperdorshme; SQL ekzekutohet vetem kur thirret ToListAsync/SingleOrDefaultAsync.
        // Kthen IQueryable, pra nje pershkrim query-je, jo ende nje liste te ngarkuar ne memorie.
        private IQueryable<UserDto> QueryUsers()
        {
            return _userManager.Users
                // Leximi nuk ndryshon entitete, ndaj nuk kemi nevoje qe EF te ndjeke ndryshimet e tyre.
                .AsNoTracking()
                .Select(user => new UserDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    ProfilePicturePath = user.ProfilePicturePath,
                    // Join lidh tabelen e anetaresimit ne role me emrat e roleve ne Identity.
                    // Select merr vetem fushat e DTO-se; PasswordHash/SecurityStamp nuk ekspozohen.
                    Roles = (from userRole in _context.UserRoles
                             join role in _context.Roles on userRole.RoleId equals role.Id
                             where userRole.UserId == user.Id && role.Name != null
                             select role.Name!).ToList()
                });
        }
    }
}
