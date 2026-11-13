namespace EmployeeManagement.Server.DTOs;

public class ProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? ProfilePicturePath { get; set; }
}
