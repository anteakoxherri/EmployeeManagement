using EmployeeManagement.Models;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Identity;
using SkiaSharp;
using EmployeeManagement.Server.Events;

namespace EmployeeManagement.Server.Services;

public class ProfileService(
    UserManager<ApplicationUser> userManager,
    IWebHostEnvironment environment,
    ILogger<ProfileService> logger,
    IRabbitMqPublisher publisher) : IProfileService
{
    public const string PictureUrlPrefix = "/uploads/profile-pictures/";
    public static string GetUploadDirectory(string contentRoot) => Path.Combine(contentRoot, "uploads", "profile-pictures");

    public async Task<ProfileDto?> GetAsync(string currentUserId)
    {
        var user = await userManager.FindByIdAsync(currentUserId);
        return user == null ? null : await ToDtoAsync(user);
    }

    public async Task<ProfileResult> UpdateAsync(string currentUserId, UpdateProfileDto request)
    {
        var user = await userManager.FindByIdAsync(currentUserId);
        if (user == null) return new() { Status = ProfileStatus.NotFound };

        // Caktojme vetem fushat e lejuara; Identity ruan telefonin ekzistues te IdentityUser.
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (user.PhoneNumber != phone) user.PhoneNumberConfirmed = false;
        user.PhoneNumber = phone;
        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
            publisher.Publish(new(BusinessEventType.ProfileUpdated, user.Id, UserId: user.Id));
        return result.Succeeded ? new() { Profile = await ToDtoAsync(user) } : Failure(result);
    }

    public async Task<ProfileResult> UploadPictureAsync(string currentUserId, IFormFile file, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(currentUserId);
        if (user == null) return new() { Status = ProfileStatus.NotFound };
        if (file.Length <= 0 || file.Length > ProfilePictureDto.MaxFileSize)
            return Invalid("The image must contain data and be no larger than 2 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
            return Invalid("Only JPG, JPEG and PNG images are allowed.");

        // Lexojme me kufi edhe nese Length nuk pasqyron madhesine reale te stream-it.
        using var buffer = new MemoryStream();
        await using (var source = file.OpenReadStream())
        {
            var chunk = new byte[81920];
            int count;
            while ((count = await source.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + count > ProfilePictureDto.MaxFileSize)
                    return Invalid("The image must be no larger than 2 MB.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
            }
        }

        using var data = SKData.CreateCopy(buffer.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec == null) return Invalid("The file is not a valid JPG, JPEG or PNG image.");
        var format = codec.EncodedFormat;
        if (!(format == SKEncodedImageFormat.Png && extension == ".png") &&
            !(format == SKEncodedImageFormat.Jpeg && extension is ".jpg" or ".jpeg"))
            return Invalid("The image content must match its JPG, JPEG or PNG extension.");
        // Kufizimi i pikseleve shmang imazhet e vogla te kompresuara qe kerkojne shume memorie.
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > 16_000_000)
            return Invalid("The image must contain at most 16 million pixels.");
        var decodedInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(decodedInfo);
        if (codec.GetPixels(decodedInfo, bitmap.GetPixels()) != SKCodecResult.Success)
            return Invalid("The file is not a valid JPG, JPEG or PNG image.");
        using (var image = SKImage.FromBitmap(bitmap))
        {
            // Emri origjinal nuk perdoret ne disk. Rikodimi heq metadata dhe permbajtje te bashkengjitur.
            var filename = $"{Guid.NewGuid():N}{(extension == ".png" ? ".png" : ".jpg")}";
            var newPath = PictureUrlPrefix + filename;
            var fullPath = Path.Combine(GetUploadDirectory(environment.ContentRootPath), filename);
            var oldPath = user.ProfilePicturePath;
            var saved = false;
            var newFileCreated = false;
            try
            {
                Directory.CreateDirectory(GetUploadDirectory(environment.ContentRootPath));
                await using (var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    newFileCreated = true;
                    using var encoded = image.Encode(format, 90);
                    if (encoded == null) return Invalid("The image could not be encoded.");
                    await output.WriteAsync(encoded.ToArray(), cancellationToken);
                }

                user.ProfilePicturePath = newPath;
                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    user.ProfilePicturePath = oldPath;
                    return Failure(result);
                }
                saved = true;
                // Dergojme vetem ID-ne; as rruga dhe as permbajtja e fotografise nuk futen ne event.
                publisher.Publish(new(BusinessEventType.ProfilePictureUpdated, user.Id, UserId: user.Id));
            }
            finally
            {
                // Deshtimi para ruajtjes ne DB heq skedarin e ri; fotoja e vjeter ruhet.
                if (!saved && newFileCreated) DeleteOwnedPicture(newPath);
            }
            // Fotoja e vjeter fshihet vetem pasi Identity konfirmon ruajtjen e rruges se re.
            DeleteOwnedPicture(oldPath);
            return new() { Profile = await ToDtoAsync(user) };
        }
    }

    private async Task<ProfileDto> ToDtoAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new()
        {
            FirstName = user.FirstName, LastName = user.LastName, PhoneNumber = user.PhoneNumber,
            Email = user.Email, Role = roles.Contains("Administrator") ? "Administrator" : roles.FirstOrDefault(),
            ProfilePicturePath = user.ProfilePicturePath
        };
    }

    private void DeleteOwnedPicture(string? relativePath)
    {
        // Fshijme vetem emrat GUID te gjeneruar nga ky service, kurre rruge arbitrare nga databaza.
        if (relativePath == null || !relativePath.StartsWith(PictureUrlPrefix, StringComparison.Ordinal)) return;
        var filename = relativePath[PictureUrlPrefix.Length..];
        var extension = Path.GetExtension(filename);
        if (extension is not (".png" or ".jpg") || filename.Length != 32 + extension.Length ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(filename), "N", out _)) return;
        try
        {
            File.Delete(Path.Combine(GetUploadDirectory(environment.ContentRootPath), filename));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Nuk u fshi fotografia e vjeter e profilit.");
        }
    }

    private static ProfileResult Invalid(string error) => new() { Status = ProfileStatus.Invalid, Errors = [error] };
    private static ProfileResult Failure(IdentityResult result) => new()
    {
        Status = result.Errors.Any(error => error.Code == "ConcurrencyFailure") ? ProfileStatus.Conflict : ProfileStatus.Invalid,
        Errors = result.Errors.Select(error => error.Description).ToArray()
    };
}
