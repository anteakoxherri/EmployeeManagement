namespace EmployeeManagement.Server.DTOs
{
    // Rezultat i brendshem midis UserService dhe UsersController, pa password ose hash.
    // Service raporton suksesin/gabimet; controller zgjedh kodin HTTP te pergjigjes.
    public class CreateUserResult
    {
        public UserDto? User { get; set; }

        public bool DuplicateEmail { get; set; }

        public string[] Errors { get; set; } = Array.Empty<string>();
    }
}
