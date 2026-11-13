using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.DTOs;

public class ProfilePictureDto
{
    public const int MaxFileSize = 2 * 1024 * 1024;

    [Required]
    public IFormFile File { get; set; } = null!;
}
