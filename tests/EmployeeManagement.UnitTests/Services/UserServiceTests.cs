using EmployeeManagement.Models;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Events;
using EmployeeManagement.Server.Services;
using EmployeeManagement.UnitTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EmployeeManagement.UnitTests.Services;

public sealed class UserServiceTests : IdentityServiceTestBase
{
    private const string ValidPassword = "KreatxIntern!2026";
    private UserService Service => new(Manager.Object, Context, Publisher.Object);

    // Kontrollon që llogaria e re ruan të dhënat, password-in e hash-uar dhe vetëm rolin Employee.
    [Fact]
    public async Task CreateUser_ValidEmployee_PersistsAccountWithEmployeeRoleAndHashedPassword()
    {
        var request = CreateRequest();
        request.Email = "  Nora.Beqiri@kreatx.example  ";

        var result = await Service.CreateUserAsync(request);

        Assert.False(result.DuplicateEmail);
        Assert.Empty(result.Errors);
        var dto = Assert.IsType<UserDto>(result.User);
        var saved = await ReadUserAsync(dto.Id);
        Assert.Equal("Nora", saved.FirstName);
        Assert.Equal("Beqiri", saved.LastName);
        Assert.Equal("Nora.Beqiri@kreatx.example", saved.Email);
        Assert.Equal(saved.Email, saved.UserName);
        Assert.Equal("NORA.BEQIRI@KREATX.EXAMPLE", saved.NormalizedEmail);
        Assert.False(string.IsNullOrWhiteSpace(saved.PasswordHash));
        Assert.NotEqual(ValidPassword, saved.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            Manager.Object.PasswordHasher.VerifyHashedPassword(saved, saved.PasswordHash!, ValidPassword));
        Assert.Equal(new[] { "Employee" }, await ReadRolesAsync(saved.Id));
        Assert.Equal(new[] { "Employee" }, dto.Roles);
        Manager.Verify(manager => manager.AddToRoleAsync(It.Is<ApplicationUser>(user => user.Id == saved.Id), "Employee"), Times.Once);
        VerifyUserEvent(BusinessEventType.UserCreated, saved.Id);
    }

    // Kontrollon që email-i ekzistues refuzohet edhe me shkronja të mëdha dhe hapësira.
    [Fact]
    public async Task CreateUser_ExistingEmailWithDifferentCasing_ReturnsDuplicateWithoutCreatingAccount()
    {
        var request = CreateRequest();
        request.Email = "  ELDA@KREATX.EXAMPLE  ";

        var result = await Service.CreateUserAsync(request);

        Assert.True(result.DuplicateEmail);
        Assert.Null(result.User);
        await AssertNoAccountCreatedAsync();
        Manager.Verify(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    // Kontrollon që roli i palejuar dhe password-i i dobët nuk krijojnë llogari ose caktim roli.
    [Theory]
    [InlineData("Contractor", ValidPassword)]
    [InlineData("Employee", "weak")]
    public async Task CreateUser_UnsupportedRoleOrWeakPassword_ReturnsErrorsWithoutPersistingAccount(string role, string password)
    {
        var request = CreateRequest();
        request.Role = role;
        request.Password = password;

        var result = await Service.CreateUserAsync(request);

        Assert.Null(result.User);
        Assert.False(result.DuplicateEmail);
        Assert.NotEmpty(result.Errors);
        await AssertNoAccountCreatedAsync();
        Manager.Verify(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    // Kontrollon që dështimi i caktimit të rolit anulon edhe krijimin e përdoruesit.
    [Fact]
    public async Task CreateUser_RoleAssignmentFails_RollsBackAccountAndPublishesNothing()
    {
        Manager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Employee"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "RoleAssignmentFailed", Description = "The Employee role could not be assigned."
            }));

        var result = await Service.CreateUserAsync(CreateRequest());

        Assert.Null(result.User);
        Assert.Contains("The Employee role could not be assigned.", result.Errors);
        Manager.Verify(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), ValidPassword), Times.Once);
        await AssertNoAccountCreatedAsync();
    }

    // Kontrollon që përditësimi ruan detajet e reja dhe heq konfirmimin e email-it kur ai ndryshon.
    [Fact]
    public async Task UpdateUser_ExistingEmployee_PersistsDetailsAndKeepsEmployeeRole()
    {
        var existing = await Context.Users.SingleAsync(user => user.Id == EmployeeId);
        existing.EmailConfirmed = true;
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var teammate = await ReadUserAsync(TeammateId);
        var request = UpdateRequest("Employee");

        var result = await Service.UpdateUserAsync(EmployeeId, request);

        Assert.Equal(UserMutationStatus.Success, result.Status);
        Assert.Equal(EmployeeId, Assert.IsType<UserDto>(result.User).Id);
        var saved = await ReadUserAsync(EmployeeId);
        Assert.Equal(request.FirstName, saved.FirstName);
        Assert.Equal(request.LastName, saved.LastName);
        Assert.Equal(request.Email.Trim(), saved.Email);
        Assert.Equal(saved.Email, saved.UserName);
        Assert.Equal(saved.Email!.ToUpperInvariant(), saved.NormalizedEmail);
        Assert.False(saved.EmailConfirmed);
        Assert.Equal(new[] { "Employee" }, await ReadRolesAsync(EmployeeId));
        await AssertUserUnchangedAsync(teammate);
        VerifyUserEvent(BusinessEventType.UserUpdated, EmployeeId);
    }

    // Kontrollon që dështimi gjatë ndryshimit të rolit rikthen profilin dhe rolin fillestar.
    [Fact]
    public async Task UpdateUser_NewRoleAssignmentFails_RollsBackDetailsAndOriginalRole()
    {
        var original = await ReadUserAsync(EmployeeId);
        Manager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Administrator"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));

        var result = await Service.UpdateUserAsync(EmployeeId, UpdateRequest("Administrator"));

        Assert.Equal(UserMutationStatus.Conflict, result.Status);
        Assert.Null(result.User);
        Assert.NotEmpty(result.Errors);
        await AssertUserUnchangedAsync(original);
        Assert.Equal(new[] { "Employee" }, await ReadRolesAsync(EmployeeId));
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që administratori i fundit nuk mund të kthehet në Employee.
    [Fact]
    public async Task UpdateUser_DemotingLastAdministrator_ReturnsConflictAndPreservesAccount()
    {
        var original = await ReadUserAsync(AdministratorId);
        var request = UpdateRequest("Employee");
        request.Email = original.Email!;

        var result = await Service.UpdateUserAsync(AdministratorId, request);

        Assert.Equal(UserMutationStatus.Conflict, result.Status);
        Assert.Contains("The last Administrator cannot be demoted.", result.Errors);
        await AssertUserUnchangedAsync(original);
        Assert.Equal(new[] { "Administrator" }, await ReadRolesAsync(AdministratorId));
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që fshirja heq anëtarësimet dhe caktimin, por ruan projektet dhe historikun e detyrës.
    [Fact]
    public async Task DeleteUser_EmployeeWithoutCreatedTasks_DeletesAccountAndPreservesTaskHistory()
    {
        var task = await SeedTaskAsync();

        var result = await Service.DeleteUserAsync(EmployeeId, AdministratorId);

        Assert.Equal(UserMutationStatus.Success, result.Status);
        await using var verificationContext = CreateReadContext();
        Assert.False(await verificationContext.Users.AnyAsync(user => user.Id == EmployeeId));
        Assert.False(await verificationContext.UserRoles.AnyAsync(role => role.UserId == EmployeeId));
        Assert.False(await verificationContext.ProjectEmployees.AnyAsync(member => member.UserId == EmployeeId));
        Assert.Equal(3, await verificationContext.Projects.CountAsync());
        Assert.True(await verificationContext.Users.AnyAsync(user => user.Id == TeammateId));
        var savedTask = await ReadTaskAsync(task.Id);
        Assert.Null(savedTask.AssignedToUserId);
        Assert.Equal(task.ProjectId, savedTask.ProjectId);
        Assert.Equal(task.CreatedByUserId, savedTask.CreatedByUserId);
        Assert.Equal(task.CreatedAt, savedTask.CreatedAt);
        VerifyUserEvent(BusinessEventType.UserDeleted, EmployeeId);
    }

    // Kontrollon ndalimin e vetëfshirjes, fshirjes së administratorit të fundit dhe fshirjes së autorit të detyrave.
    [Theory]
    [InlineData("self", UserMutationStatus.Invalid)]
    [InlineData("last-administrator", UserMutationStatus.Conflict)]
    [InlineData("task-author", UserMutationStatus.Conflict)]
    public async Task DeleteUser_RestrictedAccount_RejectsDeletionAndPreservesRelatedData(string restriction, UserMutationStatus expectedStatus)
    {
        var targetId = restriction == "task-author" ? TeammateId : AdministratorId;
        var callerId = restriction == "self" ? AdministratorId : EmployeeId;
        if (restriction == "task-author")
            await SeedTaskAsync(completed: true);
        var original = await ReadUserAsync(targetId);
        var roles = await ReadRolesAsync(targetId);
        var memberships = await Context.ProjectEmployees.CountAsync(member => member.UserId == targetId);

        var result = await Service.DeleteUserAsync(targetId, callerId);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.User);
        Assert.NotEmpty(result.Errors);
        await AssertUserUnchangedAsync(original);
        Assert.Equal(roles, await ReadRolesAsync(targetId));
        await using var verificationContext = CreateReadContext();
        Assert.Equal(memberships, await verificationContext.ProjectEmployees.CountAsync(member => member.UserId == targetId));
        if (restriction == "task-author")
            Assert.True(await verificationContext.EmployeeTasks.AnyAsync(task => task.CreatedByUserId == targetId));
        Manager.Verify(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që leximi, përditësimi dhe fshirja trajtojnë përdoruesin që nuk ekziston.
    [Fact]
    public async Task UserOperations_NonexistentUser_ReturnNullOrNotFoundWithoutPublishingEvents()
    {
        Assert.Null(await Service.GetUserByIdAsync("missing-user"));
        var updated = await Service.UpdateUserAsync("missing-user", UpdateRequest("Employee"));
        var deleted = await Service.DeleteUserAsync("missing-user", AdministratorId);

        Assert.Equal(UserMutationStatus.NotFound, updated.Status);
        Assert.Equal(UserMutationStatus.NotFound, deleted.Status);
        await using var verificationContext = CreateReadContext();
        Assert.Equal(4, await verificationContext.Users.CountAsync());
        Publisher.VerifyNoOtherCalls();
    }

    private static CreateUserDto CreateRequest() => new()
    {
        FirstName = "Nora", LastName = "Beqiri", Email = "nora.beqiri@kreatx.example",
        Password = ValidPassword, Role = "Employee"
    };

    private static UpdateUserDto UpdateRequest(string role) => new()
    {
        FirstName = "Elda Updated", LastName = "Hoxha Updated",
        Email = "  elda.updated@kreatx.example  ", Role = role
    };

    private async Task AssertNoAccountCreatedAsync()
    {
        await using var verificationContext = CreateReadContext();
        Assert.Equal(4, await verificationContext.Users.CountAsync());
        Assert.Equal(4, await verificationContext.UserRoles.CountAsync());
        Publisher.VerifyNoOtherCalls();
    }

    private async Task AssertUserUnchangedAsync(ApplicationUser original)
    {
        var saved = await ReadUserAsync(original.Id);
        Assert.Equal(
            (original.FirstName, original.LastName, original.Email, original.UserName, original.EmailConfirmed, original.PasswordHash),
            (saved.FirstName, saved.LastName, saved.Email, saved.UserName, saved.EmailConfirmed, saved.PasswordHash));
    }
}
