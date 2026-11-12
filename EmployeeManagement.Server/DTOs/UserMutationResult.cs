namespace EmployeeManagement.Server.DTOs
{
    // Rezultat i brendshem: service nuk varet nga HTTP dhe nuk ekspozon entitetin Identity.
    public enum UserMutationStatus { Success, NotFound, Invalid, Conflict }

    public class UserMutationResult
    {
        public UserMutationStatus Status { get; set; }
        public UserDto? User { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
    }
}
