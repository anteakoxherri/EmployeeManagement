using EmployeeManagement.Server.DTOs;

namespace EmployeeManagement.Server.Interfaces
{
    // Kontrata qe UsersController njeh: tregon veprimet e lejuara pa treguar si lidhemi me databazen.
    // UserService e zbaton; Program.cs lidh kete interface me implementimin permes DI.
    public interface IUserService
    {
        // Pa parametra; premton nje liste perdoruesish te sigurt pas perfundimit te punes asinkrone.
        Task<List<UserDto>> GetUsersAsync();

        // Merr ID-ne; ? tregon se rezultati mund te jete null kur perdoruesi nuk ekziston.
        Task<UserDto?> GetUserByIdAsync(string id);

        // Merr te dhenat e krijimit; kthen perdoruesin e sigurt ose arsyet pse krijimi deshtoi.
        Task<CreateUserResult> CreateUserAsync(CreateUserDto request);
    }
}
