namespace EmployeeManagement.Server.DTOs;

public enum ProfileStatus { Success, NotFound, Invalid, Conflict }

public class ProfileResult
{
    public ProfileStatus Status { get; set; }
    public ProfileDto? Profile { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
}
