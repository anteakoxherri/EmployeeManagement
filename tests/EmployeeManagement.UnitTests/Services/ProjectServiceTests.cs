using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Events;
using EmployeeManagement.Server.Services;
using EmployeeManagement.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.UnitTests.Services;

public sealed class ProjectServiceTests : ServiceTestBase
{
    private ProjectService Service => new(Context, Publisher.Object);

    // Kontrollon që prania e një detyre të hapur bllokon fshirjen dhe ruan të dhënat e projektit.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteProject_AtLeastOneOpenTask_ReturnsConflictAndPreservesProjectData(bool includeCompletedTask)
    {
        var openTask = await SeedTaskAsync();
        if (includeCompletedTask)
            await SeedTaskAsync(TeammateId, completed: true);

        var result = await Service.DeleteProjectAsync(PortalProjectId);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Null(result.Project);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        await using var verificationContext = CreateReadContext();
        Assert.True(await verificationContext.Projects.AnyAsync(project => project.Id == PortalProjectId));
        Assert.Equal(includeCompletedTask ? 2 : 1,
            await verificationContext.EmployeeTasks.CountAsync(task => task.ProjectId == PortalProjectId));
        Assert.False((await ReadTaskAsync(openTask.Id)).IsCompleted);
        Assert.Equal(2, await verificationContext.ProjectEmployees.CountAsync(member => member.ProjectId == PortalProjectId));
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që projekti bosh ose vetëm me detyra të përfunduara fshihet pa prekur përdoruesit dhe projektet e tjera.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteProject_NoOpenTasks_DeletesProjectTasksAndMembershipsWhileKeepingUsersAndOtherProjects(bool includeCompletedTask)
    {
        if (includeCompletedTask)
            await SeedTaskAsync(completed: true);
        var unrelatedTask = await SeedTaskAsync(OtherEmployeeId, PayrollProjectId);

        var result = await Service.DeleteProjectAsync(PortalProjectId);

        Assert.Equal(ProjectOperationStatus.Success, result.Status);
        await using var verificationContext = CreateReadContext();
        Assert.False(await verificationContext.Projects.AnyAsync(project => project.Id == PortalProjectId));
        Assert.False(await verificationContext.EmployeeTasks.AnyAsync(task => task.ProjectId == PortalProjectId));
        Assert.False(await verificationContext.ProjectEmployees.AnyAsync(member => member.ProjectId == PortalProjectId));
        Assert.Equal(4, await verificationContext.Users.CountAsync());
        Assert.True(await verificationContext.Projects.AnyAsync(project => project.Id == PayrollProjectId));
        Assert.True(await verificationContext.EmployeeTasks.AnyAsync(task => task.Id == unrelatedTask.Id));
        Assert.True(await verificationContext.ProjectEmployees.AnyAsync(member => member.ProjectId == PayrollProjectId));
        VerifyEvent(BusinessEventType.ProjectDeleted, PortalProjectId);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që krijimi i projektit ruan detajet dhe gjeneron ID-në dhe datën e krijimit.
    [Fact]
    public async Task CreateProject_AdministratorOperation_PersistsProjectWithGeneratedIdAndCreationTime()
    {
        var request = new ProjectRequestDto
        {
            Name = "Internship onboarding",
            Description = "Employee onboarding checklist and progress tracking."
        };
        var startedAt = DateTime.UtcNow;

        var result = await Service.CreateProjectAsync(request);

        Assert.Equal(ProjectOperationStatus.Success, result.Status);
        var dto = Assert.IsType<ProjectDto>(result.Project);
        Assert.True(dto.Id > 0);
        await using var verificationContext = CreateReadContext();
        var saved = await verificationContext.Projects.SingleAsync(project => project.Id == dto.Id);
        Assert.Equal(request.Name, saved.Name);
        Assert.Equal(request.Description, saved.Description);
        Assert.InRange(saved.CreatedAt, startedAt, DateTime.UtcNow);
        VerifyEvent(BusinessEventType.ProjectCreated, saved.Id);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që ndryshimi i detajeve ruan datën e krijimit dhe anëtarësimet e projektit.
    [Fact]
    public async Task UpdateProject_AdministratorOperation_ChangesDetailsAndPreservesCreationTimeAndMemberships()
    {
        await using var beforeContext = CreateReadContext();
        var original = await beforeContext.Projects.AsNoTracking().SingleAsync(project => project.Id == PortalProjectId);
        var request = new ProjectRequestDto
        {
            Name = "Employee self-service portal phase 2",
            Description = "Extend the portal with leave approval workflows."
        };

        var result = await Service.UpdateProjectAsync(PortalProjectId, request);

        Assert.Equal(ProjectOperationStatus.Success, result.Status);
        var dto = Assert.IsType<ProjectDto>(result.Project);
        Assert.Equal(PortalProjectId, dto.Id);
        await using var verificationContext = CreateReadContext();
        var saved = await verificationContext.Projects.SingleAsync(project => project.Id == PortalProjectId);
        Assert.Equal(request.Name, saved.Name);
        Assert.Equal(request.Description, saved.Description);
        Assert.Equal(original.CreatedAt, saved.CreatedAt);
        var members = await verificationContext.ProjectEmployees.Where(member => member.ProjectId == PortalProjectId)
            .Select(member => member.UserId).OrderBy(id => id).ToListAsync();
        Assert.Equal(new[] { TeammateId, EmployeeId }, members);
        VerifyEvent(BusinessEventType.ProjectUpdated, PortalProjectId);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që punonjësi pa detyra mund të shtohet dhe të hiqet nga projekti duke ruajtur llogarinë dhe rolin.
    [Fact]
    public async Task ManageProjectMembership_AdministratorCanAddAndRemoveEmployeeWithoutAssignedTasks()
    {
        var added = await Service.AddEmployeeAsync(PortalProjectId, OtherEmployeeId);

        Assert.Equal(ProjectOperationStatus.Success, added.Status);
        await using (var verificationContext = CreateReadContext())
            Assert.True(await verificationContext.ProjectEmployees.AnyAsync(member =>
                member.ProjectId == PortalProjectId && member.UserId == OtherEmployeeId));

        var removed = await Service.RemoveEmployeeAsync(PortalProjectId, OtherEmployeeId);

        Assert.Equal(ProjectOperationStatus.Success, removed.Status);
        await using (var verificationContext = CreateReadContext())
        {
            Assert.False(await verificationContext.ProjectEmployees.AnyAsync(member =>
                member.ProjectId == PortalProjectId && member.UserId == OtherEmployeeId));
            Assert.True(await verificationContext.Users.AnyAsync(user => user.Id == OtherEmployeeId));
            Assert.True(await verificationContext.UserRoles.AnyAsync(role =>
                role.UserId == OtherEmployeeId && role.RoleId == "role-employee"));
        }
        VerifyEvent(BusinessEventType.EmployeeAddedToProject, PortalProjectId);
        VerifyEvent(BusinessEventType.EmployeeRemovedFromProject, PortalProjectId);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që detyrat e caktuara bllokojnë heqjen e punonjësit nga projekti.
    [Fact]
    public async Task RemoveEmployee_AssignedTasksExist_ReturnsConflictAndPreservesMembership()
    {
        var assignedTask = await SeedTaskAsync();

        var result = await Service.RemoveEmployeeAsync(PortalProjectId, EmployeeId);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        await using var verificationContext = CreateReadContext();
        Assert.True(await verificationContext.ProjectEmployees.AnyAsync(member =>
            member.ProjectId == PortalProjectId && member.UserId == EmployeeId));
        Assert.Equal(EmployeeId, (await ReadTaskAsync(assignedTask.Id)).AssignedToUserId);
        Publisher.VerifyNoOtherCalls();
    }
}
