using System.Text;
using EmployeeManagement.Models;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Events;
using EmployeeManagement.Server.Services;
using EmployeeManagement.UnitTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;

namespace EmployeeManagement.UnitTests.Services;

public sealed class ProfileServiceTests : IdentityServiceTestBase
{
    private readonly TemporaryDirectory _directory = new();
    private string UploadDirectory => ProfileService.GetUploadDirectory(_directory.Path);
    private ProfileService Service => new(Manager.Object,
        Mock.Of<IWebHostEnvironment>(environment => environment.ContentRootPath == _directory.Path),
        NullLogger<ProfileService>.Instance, Publisher.Object);

    // Kontrollon që përditësimi ndryshon vetëm profilin e zgjedhur dhe ruan email-in, rolin dhe password-in.
    [Fact]
    public async Task UpdateProfile_EmployeeDetails_TrimsValuesAndPreservesIdentityAndOtherEmployee()
    {
        var user = await Context.Users.SingleAsync(user => user.Id == EmployeeId);
        user.PhoneNumber = "+355691112233";
        user.PhoneNumberConfirmed = true;
        user.EmailConfirmed = true;
        user.PasswordHash = "existing-password-hash";
        user.ProfilePicturePath = ProfileService.PictureUrlPrefix + Guid.NewGuid().ToString("N") + ".png";
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var original = await ReadUserAsync(EmployeeId);
        var teammate = await ReadUserAsync(TeammateId);

        var result = await Service.UpdateAsync(EmployeeId, UpdateRequest());

        Assert.Equal(ProfileStatus.Success, result.Status);
        var dto = Assert.IsType<ProfileDto>(result.Profile);
        var saved = await ReadUserAsync(EmployeeId);
        Assert.Equal("Elda Updated", saved.FirstName);
        Assert.Equal("Hoxha", saved.LastName);
        Assert.Equal("+355 69 123 4567", saved.PhoneNumber);
        Assert.False(saved.PhoneNumberConfirmed);
        Assert.Equal(saved.FirstName, dto.FirstName);
        Assert.Equal(original.Email, saved.Email);
        Assert.Equal(original.UserName, saved.UserName);
        Assert.Equal(original.EmailConfirmed, saved.EmailConfirmed);
        Assert.Equal(original.PasswordHash, saved.PasswordHash);
        Assert.Equal(original.ProfilePicturePath, saved.ProfilePicturePath);
        Assert.Equal(new[] { "Employee" }, await ReadRolesAsync(EmployeeId));
        var otherSaved = await ReadUserAsync(TeammateId);
        Assert.Equal(teammate.FirstName, otherSaved.FirstName);
        Assert.Equal(teammate.LastName, otherSaved.LastName);
        Assert.Equal(teammate.PhoneNumber, otherSaved.PhoneNumber);
        VerifyUserEvent(BusinessEventType.ProfileUpdated, EmployeeId);
    }

    // Kontrollon që gabimet e Identity kthehen si Invalid ose Conflict pa ruajtur ndryshime.
    [Theory]
    [InlineData("InvalidUserName", ProfileStatus.Invalid)]
    [InlineData("ConcurrencyFailure", ProfileStatus.Conflict)]
    public async Task UpdateProfile_IdentityRejectsUpdate_ReturnsFailureWithoutPublishing(string code, ProfileStatus expectedStatus)
    {
        var original = await ReadUserAsync(EmployeeId);
        Manager.Setup(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = code, Description = "Profile update rejected." }));

        var result = await Service.UpdateAsync(EmployeeId, UpdateRequest());

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Profile);
        Assert.Contains("Profile update rejected.", result.Errors);
        var saved = await ReadUserAsync(EmployeeId);
        Assert.Equal(original.FirstName, saved.FirstName);
        Assert.Equal(original.LastName, saved.LastName);
        Assert.Equal(original.PhoneNumber, saved.PhoneNumber);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që përdoruesi që mungon nuk mund të lexohet, përditësohet ose të marrë fotografi.
    [Fact]
    public async Task ProfileOperations_NonexistentUser_ReturnNotFoundAndCreateNoFiles()
    {
        using var stream = new MemoryStream(TestPictures.Create());
        var file = new FormFile(stream, 0, stream.Length, "file", "portrait.png");

        Assert.Null(await Service.GetAsync("missing-user"));
        var updated = await Service.UpdateAsync("missing-user", UpdateRequest());
        var uploaded = await Service.UploadPictureAsync("missing-user", file, CancellationToken.None);

        Assert.Equal(ProfileStatus.NotFound, updated.Status);
        Assert.Equal(ProfileStatus.NotFound, uploaded.Status);
        Assert.Empty(Directory.GetFiles(_directory.Path, "*", SearchOption.AllDirectories));
        Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që PNG/JPEG ruhet me emër të sigurt te përdoruesi i duhur dhe zëvendëson vetëm fotografinë e tij.
    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "../../client-name.PNG", ".png")]
    [InlineData(SKEncodedImageFormat.Jpeg, "../../client-name.JPEG", ".jpg")]
    public async Task UploadPicture_ValidImage_AssociatesWithEmployeeAndSafelyReplacesOwnPicture(
        SKEncodedImageFormat format, string clientName, string extension)
    {
        var oldPath = await ArrangeExistingPictureAsync(EmployeeId);
        var teammatePath = await ArrangeExistingPictureAsync(TeammateId);
        using var stream = new MemoryStream(TestPictures.Create(format));
        var file = new FormFile(stream, 0, stream.Length, "file", clientName);

        var result = await Service.UploadPictureAsync(EmployeeId, file, CancellationToken.None);

        Assert.Equal(ProfileStatus.Success, result.Status);
        var profile = Assert.IsType<ProfileDto>(result.Profile);
        var saved = await ReadUserAsync(EmployeeId);
        Assert.Equal(profile.ProfilePicturePath, saved.ProfilePicturePath);
        Assert.StartsWith(ProfileService.PictureUrlPrefix, saved.ProfilePicturePath);
        var filename = Path.GetFileName(saved.ProfilePicturePath!);
        Assert.Equal(extension, Path.GetExtension(filename));
        Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(filename), "N", out _));
        Assert.DoesNotContain("client-name", filename);
        var savedFile = Path.Combine(UploadDirectory, filename);
        Assert.True(File.Exists(savedFile));
        using var data = SKData.CreateCopy(await File.ReadAllBytesAsync(savedFile));
        using var codec = SKCodec.Create(data);
        Assert.NotNull(codec);
        Assert.Equal(format, codec.EncodedFormat);
        Assert.False(File.Exists(FullPicturePath(oldPath)));
        Assert.Equal(teammatePath, (await ReadUserAsync(TeammateId)).ProfilePicturePath);
        Assert.True(File.Exists(FullPicturePath(teammatePath)));
        Assert.Equal(2, Directory.GetFiles(UploadDirectory).Length);
        VerifyUserEvent(BusinessEventType.ProfilePictureUpdated, EmployeeId);
    }

    // Kontrollon që skedarët bosh, jo-imazhe, me format të gabuar ose prapashtesë të palejuar refuzohen.
    [Theory]
    [InlineData("unsupported-extension")]
    [InlineData("not-an-image")]
    [InlineData("mismatched-format")]
    [InlineData("empty")]
    public async Task UploadPicture_InvalidFile_PreservesExistingPictureAndPublishesNothing(string scenario)
    {
        var oldPath = await ArrangeExistingPictureAsync(EmployeeId);
        var content = scenario switch
        {
            "empty" => Array.Empty<byte>(),
            "not-an-image" => Encoding.UTF8.GetBytes("This is not an image."),
            _ => TestPictures.Create()
        };
        var filename = scenario switch
        {
            "unsupported-extension" => "portrait.gif",
            "mismatched-format" => "portrait.jpg",
            _ => "portrait.png"
        };
        using var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, stream.Length, "file", filename);

        var result = await Service.UploadPictureAsync(EmployeeId, file, CancellationToken.None);

        await AssertPictureRejectedAsync(result, ProfileStatus.Invalid, oldPath);
        Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    // Kontrollon kufirin 2 MB edhe kur gjatësia e deklaruar e skedarit është e pasaktë.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadPicture_ExceedsSizeLimit_RejectsReportedAndActualStreamSize(bool underreportedLength)
    {
        var oldPath = await ArrangeExistingPictureAsync(EmployeeId);
        using var stream = new MemoryStream(new byte[ProfilePictureDto.MaxFileSize + 1]);
        var file = new Mock<IFormFile>(MockBehavior.Strict);
        file.SetupGet(upload => upload.Length).Returns(underreportedLength ? 1 : stream.Length);
        file.SetupGet(upload => upload.FileName).Returns("portrait.png");
        file.Setup(upload => upload.OpenReadStream()).Returns(stream);

        var result = await Service.UploadPictureAsync(EmployeeId, file.Object, CancellationToken.None);

        await AssertPictureRejectedAsync(result, ProfileStatus.Invalid, oldPath);
        file.Verify(upload => upload.OpenReadStream(), underreportedLength ? Times.Once() : Times.Never());
        Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    // Kontrollon që dështimi i ruajtjes në Identity fshin skedarin e ri dhe ruan fotografinë e vjetër.
    [Fact]
    public async Task UploadPicture_IdentitySaveFails_RemovesNewFileAndRestoresOldAssociation()
    {
        var oldPath = await ArrangeExistingPictureAsync(EmployeeId);
        Manager.Setup(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));
        using var stream = new MemoryStream(TestPictures.Create());
        var file = new FormFile(stream, 0, stream.Length, "file", "replacement.png");

        var result = await Service.UploadPictureAsync(EmployeeId, file, CancellationToken.None);

        await AssertPictureRejectedAsync(result, ProfileStatus.Conflict, oldPath);
        Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Once);
        Assert.Equal(oldPath, (await Manager.Object.FindByIdAsync(EmployeeId))!.ProfilePicturePath);
    }

    public override async Task DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { _directory.Dispose(); }
    }

    private static UpdateProfileDto UpdateRequest() => new()
    {
        FirstName = "  Elda Updated  ", LastName = "  Hoxha  ", PhoneNumber = "  +355 69 123 4567  "
    };

    private async Task<string> ArrangeExistingPictureAsync(string userId)
    {
        var relativePath = ProfileService.PictureUrlPrefix + Guid.NewGuid().ToString("N") + ".png";
        Directory.CreateDirectory(UploadDirectory);
        await File.WriteAllBytesAsync(FullPicturePath(relativePath), TestPictures.Create());
        var user = await Context.Users.SingleAsync(user => user.Id == userId);
        user.ProfilePicturePath = relativePath;
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return relativePath;
    }

    private string FullPicturePath(string relativePath) => Path.Combine(UploadDirectory, Path.GetFileName(relativePath));

    private async Task AssertPictureRejectedAsync(ProfileResult result, ProfileStatus expectedStatus, string originalPath)
    {
        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Profile);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(originalPath, (await ReadUserAsync(EmployeeId)).ProfilePicturePath);
        Assert.True(File.Exists(FullPicturePath(originalPath)));
        Assert.Single(Directory.GetFiles(UploadDirectory));
        Publisher.VerifyNoOtherCalls();
    }
}
