using EmployeeManagement.Models;
using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using EmployeeManagement.Server.Events;

namespace EmployeeManagement.Server.Services
{
    // Zbaton kontraten IUserService dhe mban logjiken e perdoruesve jashte controller-it.
    // UserManager ofron rregullat e Identity; DbContext mundeson query EF Core dhe transaksionet.
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IRabbitMqPublisher _publisher;

        // DI jep te njejtin DbContext te scope-it edhe te Identity, qe shkrimet te ndajne transaksionin.
        public UserService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IRabbitMqPublisher publisher)
        {
            _userManager = userManager;
            _context = context;
            _publisher = publisher;
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
                // Publikimi pranohet ne background vetem pasi databaza konfirmon transaksionin.
                _publisher.Publish(new(BusinessEventType.UserCreated, user.Id, UserId: user.Id));
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

        // Ndryshon profilin dhe rolin ne nje transaksion: deshtimi i Identity anulon te gjitha shkrimet.
        public async Task<UserMutationResult> UpdateUserAsync(string id, UpdateUserDto request)
        {
            if (request.Role != "Employee" && request.Role != "Administrator")
                return MutationFailure(UserMutationStatus.Invalid, "Role must be Employee or Administrator.");

            try
            {
                // Serializable mbron kontrollin e administratorit te fundit nga kerkesa te njekohshme.
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return MutationFailure(UserMutationStatus.NotFound);

                var email = request.Email.Trim();
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null && existingUser.Id != id)
                    return MutationFailure(UserMutationStatus.Conflict, "A user with this email already exists.");

                if (!await _context.Roles.AnyAsync(role => role.NormalizedName == request.Role.ToUpperInvariant()))
                    return MutationFailure(UserMutationStatus.Invalid, "The selected role does not exist.");

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Administrator") && request.Role != "Administrator" &&
                    await CountAdministratorsAsync() <= 1)
                    return MutationFailure(UserMutationStatus.Conflict, "The last Administrator cannot be demoted.");

                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                // UserName mbetet email-i, si te krijimi; UpdateAsync normalizon te dyja fushat.
                if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                    user.EmailConfirmed = false;
                user.Email = email;
                user.UserName = email;

                var update = await _userManager.UpdateAsync(user);
                if (!update.Succeeded)
                    return IdentityFailure(update);

                var rolesToRemove = roles.Where(role => role != request.Role).ToArray();
                if (rolesToRemove.Length > 0)
                {
                    var removal = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!removal.Succeeded)
                        return IdentityFailure(removal);
                }
                if (!roles.Contains(request.Role))
                {
                    var assignment = await _userManager.AddToRoleAsync(user, request.Role);
                    if (!assignment.Succeeded)
                        return IdentityFailure(assignment);
                }

                var updatedUser = await GetUserByIdAsync(id);
                await transaction.CommitAsync();
                _publisher.Publish(new(BusinessEventType.UserUpdated, id, UserId: id));
                return new UserMutationResult { User = updatedUser };
            }
            catch (DbUpdateConcurrencyException)
            {
                return MutationFailure(UserMutationStatus.Conflict, "The user changed. Reload and try again.");
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return MutationFailure(UserMutationStatus.Conflict, "A user with this email already exists.");
            }
            catch (Exception exception) when (IsRelationshipOrDeadlockConflict(exception))
            {
                return MutationFailure(UserMutationStatus.Conflict, "Related data changed. Reload and try again.");
            }
        }

        // Fshin vetem perdoruesin; projektet dhe historiku i detyrave nuk fshihen.
        public async Task<UserMutationResult> DeleteUserAsync(string id, string currentUserId)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return MutationFailure(UserMutationStatus.NotFound);

                if (id == currentUserId)
                    return MutationFailure(UserMutationStatus.Invalid, "You cannot delete your own account.");

                if (await _userManager.IsInRoleAsync(user, "Administrator") && await CountAdministratorsAsync() <= 1)
                    return MutationFailure(UserMutationStatus.Conflict, "The last Administrator cannot be deleted.");

                // CreatedBy ka Restrict: ndalojme fshirjen per te ruajtur autorin e historikut.
                if (await _context.EmployeeTasks.AnyAsync(task => task.CreatedByUserId == id))
                    return MutationFailure(UserMutationStatus.Conflict, "This user has created tasks and must be retained for task history.");

                // FK-te ekzistuese ne SQL heqin vetem anetaresimet (Cascade) dhe vendosin
                // AssignedToUserId = null (SetNull). Projektet, detyrat dhe datat e tyre ruhen.
                var deletion = await _userManager.DeleteAsync(user);
                if (!deletion.Succeeded)
                    return IdentityFailure(deletion);

                await transaction.CommitAsync();
                _publisher.Publish(new(BusinessEventType.UserDeleted, id, UserId: id));
                return new UserMutationResult();
            }
            catch (DbUpdateConcurrencyException)
            {
                return MutationFailure(UserMutationStatus.Conflict, "The user changed. Reload and try again.");
            }
            catch (Exception exception) when (IsRelationshipOrDeadlockConflict(exception))
            {
                return MutationFailure(UserMutationStatus.Conflict, "Related data prevents deletion or changed. Reload and try again.");
            }
        }

        // Ky query ndan te njejtin transaksion me ndryshimin/fshirjen e roleve.
        private Task<int> CountAdministratorsAsync()
        {
            return (from membership in _context.UserRoles
                    join role in _context.Roles on membership.RoleId equals role.Id
                    where role.NormalizedName == "ADMINISTRATOR"
                    select membership.UserId).CountAsync();
        }

        private static UserMutationResult MutationFailure(UserMutationStatus status, params string[] errors)
            => new() { Status = status, Errors = errors };

        // Gabimet e validimit japin 400; dublikatat dhe konflikti optimist japin 409.
        private static UserMutationResult IdentityFailure(IdentityResult result)
            => MutationFailure(result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName" or "ConcurrencyFailure")
                ? UserMutationStatus.Conflict : UserMutationStatus.Invalid,
                result.Errors.Select(error => error.Description).ToArray());

        // Nje FK ose deadlock nga nje kerkese konkurrente kthehet si konflikt pa zbuluar SQL-ne.
        private static bool IsRelationshipOrDeadlockConflict(Exception exception)
        {
            // EF mund ta mbeshtjelle SqlException me DbUpdateException dhe InvalidOperationException.
            for (Exception? cause = exception; cause != null; cause = cause.InnerException)
            {
                if (cause is SqlException { Number: 547 or 1205 })
                    return true;
            }
            return false;
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
