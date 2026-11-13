using System.Security.Claims;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Server.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(Roles = "Employee,Administrator")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    // ID merret vetem nga JWT i verifikuar; klienti nuk zgjedh perdoruesin ne URL ose body.
    [HttpGet("me")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileDto>> GetMe()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        var profile = await profileService.GetAsync(id);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileDto>> UpdateMe(UpdateProfileDto request)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        return ToResponse(await profileService.UpdateAsync(id, request));
    }

    [HttpPost("me/picture")]
    [Consumes("multipart/form-data")]
    // Kufiri HTTP perfshin hapesire per headers multipart; service kontrollon kufirin ekzakt te skedarit.
    [RequestSizeLimit(ProfilePictureDto.MaxFileSize + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfilePictureDto.MaxFileSize + 64 * 1024)]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ProfileDto>> UploadPicture([FromForm] ProfilePictureDto request, CancellationToken cancellationToken)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        return ToResponse(await profileService.UploadPictureAsync(id, request.File, cancellationToken));
    }

    private ActionResult<ProfileDto> ToResponse(ProfileResult result) => result.Status switch
    {
        ProfileStatus.NotFound => NotFound(),
        ProfileStatus.Invalid => BadRequest(new { errors = result.Errors }),
        ProfileStatus.Conflict => Conflict(new { errors = result.Errors }),
        _ => Ok(result.Profile)
    };
}
