using EmployeeManagement.Server.DTOs;

namespace EmployeeManagement.Server.Interfaces;

public interface IProfileService
{
    Task<ProfileDto?> GetAsync(string currentUserId);
    Task<ProfileResult> UpdateAsync(string currentUserId, UpdateProfileDto request);
    Task<ProfileResult> UploadPictureAsync(string currentUserId, IFormFile file, CancellationToken cancellationToken);
}
