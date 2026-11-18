using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EmployeeManagement.Models;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Events;
using EmployeeManagement.Server.Services;
using EmployeeManagement.UnitTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EmployeeManagement.UnitTests.Controllers;

// These tests exercise actual MVC filters and controller identity selection.
// The services and Identity store still use the isolated SQLite fixture.
public sealed class AccountBoundaryTests : IdentityServiceTestBase
{
    private readonly TemporaryDirectory _directory = new();
    private AccountApiHost _api = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _api = await AccountApiHost.StartAsync(Context, Manager.Object, Publisher.Object, _directory.Path);
    }

    // Kontrollon që kërkesa e administratorit krijon Employee dhe kthen përgjigje të sigurt HTTP 201.
    [Fact]
    public async Task CreateEmployee_AuthenticatedAdministrator_CreatesAccountThroughManagementEndpoint()
    {
        _api.SignIn(AdministratorId, "Administrator");
        var request = CreateEmployeeRequest();

        using var response = await _api.Client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.Password, body);
        var dto = Assert.IsType<UserDto>(await response.Content.ReadFromJsonAsync<UserDto>());
        Assert.Equal(request.Email, (await ReadUserAsync(dto.Id)).Email);
        Assert.Equal(new[] { "Employee" }, await ReadRolesAsync(dto.Id));
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/users/{dto.Id}", response.Headers.Location.AbsolutePath);
        VerifyUserEvent(BusinessEventType.UserCreated, dto.Id);
    }

    // Kontrollon që Employee nuk mund të krijojë, ndryshojë ose fshijë përdorues dhe që krijimi kërkon autentikim.
    [Theory]
    [InlineData("create", "Employee", HttpStatusCode.Forbidden)]
    [InlineData("update", "Employee", HttpStatusCode.Forbidden)]
    [InlineData("delete", "Employee", HttpStatusCode.Forbidden)]
    [InlineData("create", null, HttpStatusCode.Unauthorized)]
    public async Task ManageUsers_CallerIsNotAdministrator_AuthorizationBlocksMutation(
        string operation, string? role, HttpStatusCode expectedStatus)
    {
        if (role is not null)
            _api.SignIn(EmployeeId, role);
        var original = await ReadUserAsync(TeammateId);

        using var response = operation switch
        {
            "create" => await _api.Client.PostAsJsonAsync("/api/users", CreateEmployeeRequest()),
            "update" => await _api.Client.PutAsJsonAsync($"/api/users/{TeammateId}", new UpdateUserDto
            {
                FirstName = "Changed", LastName = "User", Email = "changed@kreatx.example", Role = "Employee"
            }),
            "delete" => await _api.Client.DeleteAsync($"/api/users/{TeammateId}"),
            _ => throw new ArgumentException("Unknown test operation.", nameof(operation))
        };

        Assert.Equal(expectedStatus, response.StatusCode);
        await using var verificationContext = CreateReadContext();
        Assert.Equal(4, await verificationContext.Users.CountAsync());
        await AssertProfileUnchangedAsync(original);
        Manager.Verify(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Manager.Verify(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që MVC refuzon email-in e pavlefshëm para thirrjes së Identity dhe ruajtjes së llogarisë.
    [Fact]
    public async Task CreateEmployee_InvalidEmail_MvcRejectsRequestBeforeIdentityRuns()
    {
        _api.SignIn(AdministratorId, "Administrator");
        var request = CreateEmployeeRequest();
        request.Email = "not-an-email";

        using var response = await _api.Client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(await response.Content.ReadFromJsonAsync<ValidationProblemDetails>());
        Assert.Contains("Email", problem.Errors.Keys);
        await using var verificationContext = CreateReadContext();
        Assert.Equal(4, await verificationContext.Users.CountAsync());
        Manager.Verify(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që ID-ja në query nuk mund ta drejtojë ndryshimin e profilit ose fotografisë te një koleg.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfileMutation_QueryTargetsAnotherEmployee_OnlyChangesAuthenticatedEmployee(bool uploadPicture)
    {
        _api.SignIn(EmployeeId, "Employee");
        var teammate = await ReadUserAsync(TeammateId);
        var query = $"?userId={TeammateId}&id={TeammateId}";
        HttpResponseMessage response;
        if (uploadPicture)
        {
            using var form = new MultipartFormDataContent();
            var picture = new ByteArrayContent(TestPictures.Create());
            picture.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(picture, "file", "portrait.png");
            response = await _api.Client.PostAsync("/api/profile/me/picture" + query, form);
        }
        else
        {
            response = await _api.Client.PutAsJsonAsync("/api/profile/me" + query, ValidProfileRequest());
        }

        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var profile = Assert.IsType<ProfileDto>(await response.Content.ReadFromJsonAsync<ProfileDto>());
            Assert.Equal("elda@kreatx.example", profile.Email);
        }
        var saved = await ReadUserAsync(EmployeeId);
        if (uploadPicture)
        {
            Assert.NotNull(saved.ProfilePicturePath);
            Assert.True(File.Exists(Path.Combine(ProfileService.GetUploadDirectory(_directory.Path), Path.GetFileName(saved.ProfilePicturePath))));
        }
        else
        {
            Assert.Equal("Elda Updated", saved.FirstName);
            Assert.Equal("+355 69 123 4567", saved.PhoneNumber);
        }
        await AssertProfileUnchangedAsync(teammate);
        Manager.Verify(manager => manager.UpdateAsync(It.Is<ApplicationUser>(user => user.Id == EmployeeId)), Times.Once);
        Manager.Verify(manager => manager.UpdateAsync(It.Is<ApplicationUser>(user => user.Id == TeammateId)), Times.Never);
        VerifyUserEvent(uploadPicture ? BusinessEventType.ProfilePictureUpdated : BusinessEventType.ProfileUpdated, EmployeeId);
    }

    // Kontrollon që dërgimi i ID-së së një kolegu në JSON refuzohet pa ndryshuar asnjërin profil.
    [Fact]
    public async Task UpdateProfile_BodyAttemptsToSelectAnotherUser_RejectsUnmappedId()
    {
        _api.SignIn(EmployeeId, "Employee");
        var original = await ReadUserAsync(EmployeeId);
        var teammate = await ReadUserAsync(TeammateId);
        var body = new { firstName = "Changed", lastName = "Profile", id = TeammateId };

        using var response = await _api.Client.PutAsJsonAsync("/api/profile/me", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProfileUnchangedAsync(original);
        await AssertProfileUnchangedAsync(teammate);
        Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që MVC refuzon emrin bosh, mbi kufirin e gjatësisë dhe numrin e pavlefshëm të telefonit.
    [Theory]
    [InlineData("blank-name", "FirstName")]
    [InlineData("long-name", "LastName")]
    [InlineData("invalid-phone", "PhoneNumber")]
    public async Task UpdateProfile_InvalidInput_MvcRejectsRequestWithoutChangingProfile(string scenario, string expectedField)
    {
        _api.SignIn(EmployeeId, "Employee");
        var original = await ReadUserAsync(EmployeeId);
        var request = ValidProfileRequest();
        if (scenario == "blank-name") request.FirstName = "   ";
        if (scenario == "long-name") request.LastName = new string('a', 101);
        if (scenario == "invalid-phone") request.PhoneNumber = "not-a-phone";

        using var response = await _api.Client.PutAsJsonAsync("/api/profile/me", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(await response.Content.ReadFromJsonAsync<ValidationProblemDetails>());
        Assert.Contains(expectedField, problem.Errors.Keys);
        await AssertProfileUnchangedAsync(original);
        Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që përditësimi i profilit kërkon autentikim dhe nuk lejon ndryshime anonime.
    [Fact]
    public async Task UpdateProfile_AnonymousCaller_ReturnsUnauthorizedWithoutChangingUser()
    {
        var original = await ReadUserAsync(EmployeeId);

        using var response = await _api.Client.PutAsJsonAsync("/api/profile/me", ValidProfileRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProfileUnchangedAsync(original);
        Manager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Publisher.VerifyNoOtherCalls();
    }

    public override async Task DisposeAsync()
    {
        try { _api?.Dispose(); }
        finally
        {
            try { await base.DisposeAsync(); }
            finally { _directory.Dispose(); }
        }
    }

    private static CreateUserDto CreateEmployeeRequest() => new()
    {
        FirstName = "Nora", LastName = "Beqiri", Email = "nora.beqiri@kreatx.example",
        Password = "KreatxIntern!2026", Role = "Employee"
    };

    private static UpdateProfileDto ValidProfileRequest() => new()
    {
        FirstName = "Elda Updated", LastName = "Hoxha", PhoneNumber = "+355 69 123 4567"
    };

    private async Task AssertProfileUnchangedAsync(ApplicationUser original)
    {
        var saved = await ReadUserAsync(original.Id);
        Assert.Equal(
            (original.FirstName, original.LastName, original.PhoneNumber, original.Email, original.ProfilePicturePath),
            (saved.FirstName, saved.LastName, saved.PhoneNumber, saved.Email, saved.ProfilePicturePath));
    }
}
