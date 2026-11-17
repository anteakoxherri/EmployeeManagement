using EmployeeManagement.Models;
using EmployeeManagement.Server.DTOs;
using EmployeeManagement.Server.Events;
using EmployeeManagement.Server.Services;
using EmployeeManagement.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.UnitTests.Services;

public sealed class TaskServiceTests : ServiceTestBase
{
    private TaskService Service => new(Context, Publisher.Object);

    // Kontrollon që krijimi refuzohet kur projekti mungon ose punonjësi nuk është anëtar.
    [Theory]
    [InlineData(PayrollProjectId)]
    [InlineData(999)]
    public async Task CreateTask_EmployeeOutsideProjectOrProjectMissing_ReturnsNotFoundWithoutCreatingTask(int projectId)
    {
        var result = await Service.CreateTaskAsync(CreateRequest(projectId), EmployeeId, isAdministrator: false);

        await AssertCreationRejectedAsync(result, TaskOperationStatus.NotFound);
    }

    // Kontrollon që anëtari mund të krijojë detyrë për vete, për kolegun ose pa caktim.
    [Theory]
    [InlineData(EmployeeId)]
    [InlineData(TeammateId)]
    [InlineData(null)]
    public async Task CreateTask_EmployeeInProject_CanAssignSelfOrTeammateOrLeaveUnassigned(string? assigneeId)
    {
        var request = CreateRequest(PortalProjectId, assigneeId);
        var startedAt = DateTime.UtcNow;

        var result = await Service.CreateTaskAsync(request, EmployeeId, isAdministrator: false);

        Assert.Equal(TaskOperationStatus.Success, result.Status);
        var dto = Assert.IsType<TaskDto>(result.Task);
        var saved = await ReadTaskAsync(dto.Id);
        Assert.Equal(request.Title, saved.Title);
        Assert.Equal(request.Description, saved.Description);
        Assert.Equal(PortalProjectId, saved.ProjectId);
        Assert.Equal(assigneeId, saved.AssignedToUserId);
        Assert.Equal(EmployeeId, saved.CreatedByUserId);
        Assert.InRange(saved.CreatedAt, startedAt, DateTime.UtcNow);
        Assert.False(saved.IsCompleted);
        Assert.Null(saved.CompletedAt);
        VerifyEvent(BusinessEventType.TaskCreated, PortalProjectId, saved.Id);
        if (assigneeId is not null)
            VerifyEvent(BusinessEventType.TaskAssigned, PortalProjectId, saved.Id);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që caktimi refuzohet për persona jashtë projektit ose pa rolin Employee.
    [Theory]
    [InlineData(OtherEmployeeId)]
    [InlineData(AdministratorId)]
    public async Task CreateTask_AssigneeIsOutsideProjectOrLacksEmployeeRole_ReturnsInvalid(string assigneeId)
    {
        // The administrator is a project member but has no Employee role.
        Context.ProjectEmployees.Add(new ProjectEmployee { ProjectId = PortalProjectId, UserId = AdministratorId });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var result = await Service.CreateTaskAsync(CreateRequest(PortalProjectId, assigneeId), EmployeeId, isAdministrator: false);

        await AssertCreationRejectedAsync(result, TaskOperationStatus.Invalid);
    }

    // Kontrollon që punonjësi nuk mund të ndryshojë ose të marrë detyrën e një kolegu.
    [Theory]
    [InlineData(TeammateId)]
    [InlineData(EmployeeId)]
    public async Task UpdateTask_AssignedToAnotherEmployee_RejectsEditsAndAttemptedTakeover(string requestedAssigneeId)
    {
        var original = await SeedTaskAsync(TeammateId);
        var request = UpdateRequest(original);
        request.AssignedToUserId = requestedAssigneeId;

        var result = await Service.UpdateTaskAsync(original.Id, request, EmployeeId, isAdministrator: false);

        await AssertUpdateRejectedAsync(result, TaskOperationStatus.Forbidden, original);
    }

    // Kontrollon që punonjësi nuk mund të ndryshojë projektin ose caktimin e detyrës së vet.
    [Theory]
    [InlineData(PortalProjectId, TeammateId)]
    [InlineData(PortalProjectId, null)]
    [InlineData(ReportingProjectId, EmployeeId)]
    public async Task UpdateTask_EmployeeChangesOwnTaskAssigneeOrProject_ReturnsForbidden(int projectId, string? assigneeId)
    {
        var original = await SeedTaskAsync();
        var request = UpdateRequest(original);
        request.ProjectId = projectId;
        request.AssignedToUserId = assigneeId;
        // Elda also belongs to Reporting: membership does not permit moving an existing task.

        var result = await Service.UpdateTaskAsync(original.Id, request, EmployeeId, isAdministrator: false);

        await AssertUpdateRejectedAsync(result, TaskOperationStatus.Forbidden, original);
    }

    // Kontrollon që punonjësi mund të ndryshojë detajet dhe të përfundojë detyrën e vet.
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    public async Task UpdateTask_EmployeeEditsOwnTask_PersistsDetailsAndRequestedCompletion(bool? completed)
    {
        var original = await SeedTaskAsync();
        var request = UpdateRequest(original);
        request.IsCompleted = completed;

        var result = await Service.UpdateTaskAsync(original.Id, request, EmployeeId, isAdministrator: false);

        Assert.Equal(TaskOperationStatus.Success, result.Status);
        var saved = await ReadTaskAsync(original.Id);
        Assert.Equal(request.Title, saved.Title);
        Assert.Equal(request.Description, saved.Description);
        Assert.Equal(original.ProjectId, saved.ProjectId);
        Assert.Equal(original.AssignedToUserId, saved.AssignedToUserId);
        Assert.Equal(original.CreatedAt, saved.CreatedAt);
        Assert.Equal(original.CreatedByUserId, saved.CreatedByUserId);
        Assert.Equal(completed ?? false, saved.IsCompleted);
        Assert.Equal(completed == true, saved.CompletedAt.HasValue);
        VerifyEvent(BusinessEventType.TaskUpdated, PortalProjectId, original.Id);
        if (completed == true)
            VerifyEvent(BusinessEventType.TaskCompleted, PortalProjectId, original.Id);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që ndryshimi dhe përfundimi refuzohen jashtë projekteve të punonjësit.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModifyTask_EmployeeOutsideProject_ReturnsNotFoundAndPreservesTask(bool completeTask)
    {
        var original = await SeedTaskAsync(OtherEmployeeId, PayrollProjectId);

        var result = completeTask
            ? await Service.CompleteTaskAsync(original.Id, EmployeeId, isAdministrator: false)
            : await Service.UpdateTaskAsync(original.Id, UpdateRequest(original), EmployeeId, isAdministrator: false);

        await AssertUpdateRejectedAsync(result, TaskOperationStatus.NotFound, original);
    }

    // Kontrollon që punonjësi nuk mund të përfundojë detyrën e kolegut ose një detyrë pa caktim.
    [Theory]
    [InlineData(TeammateId)]
    [InlineData(null)]
    public async Task CompleteTask_OtherEmployeesTaskOrUnassignedTask_ReturnsForbidden(string? assigneeId)
    {
        var original = await SeedTaskAsync(assigneeId);

        var result = await Service.CompleteTaskAsync(original.Id, EmployeeId, isAdministrator: false);

        await AssertUpdateRejectedAsync(result, TaskOperationStatus.Forbidden, original);
    }

    // Kontrollon që përfundimi i përsëritur ruan datën e parë dhe publikon vetëm një event.
    [Fact]
    public async Task CompleteTask_EmployeeOwnsTask_CompletesOnceAndPreservesFirstCompletionTimeOnRetry()
    {
        var original = await SeedTaskAsync();
        var startedAt = DateTime.UtcNow;

        var first = await Service.CompleteTaskAsync(original.Id, EmployeeId, isAdministrator: false);
        var firstSaved = await ReadTaskAsync(original.Id);
        var repeated = await Service.CompleteTaskAsync(original.Id, EmployeeId, isAdministrator: false);

        Assert.Equal(TaskOperationStatus.Success, first.Status);
        Assert.Equal(TaskOperationStatus.Success, repeated.Status);
        var saved = await ReadTaskAsync(original.Id);
        Assert.True(saved.IsCompleted);
        Assert.NotNull(saved.CompletedAt);
        Assert.InRange(saved.CompletedAt.Value, startedAt, DateTime.UtcNow);
        Assert.Equal(firstSaved.CompletedAt, saved.CompletedAt);
        VerifyEvent(BusinessEventType.TaskCompleted, PortalProjectId, original.Id);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që punonjësi nuk mund të rihapë detyrën e vet të përfunduar.
    [Fact]
    public async Task UpdateTask_EmployeeReopensOwnCompletedTask_ReturnsForbidden()
    {
        var original = await SeedTaskAsync(completed: true);
        var request = UpdateRequest(original);
        request.IsCompleted = false;

        var result = await Service.UpdateTaskAsync(original.Id, request, EmployeeId, isAdministrator: false);

        await AssertUpdateRejectedAsync(result, TaskOperationStatus.Forbidden, original);
    }

    // Kontrollon që administratori mund të krijojë dhe të caktojë detyrë edhe pa anëtarësi në projekt.
    [Fact]
    public async Task CreateTask_AdministratorOutsideProject_CanCreateAndAssignToProjectEmployee()
    {
        var result = await Service.CreateTaskAsync(CreateRequest(PayrollProjectId, OtherEmployeeId), AdministratorId, isAdministrator: true);

        Assert.Equal(TaskOperationStatus.Success, result.Status);
        var dto = Assert.IsType<TaskDto>(result.Task);
        var saved = await ReadTaskAsync(dto.Id);
        Assert.Equal(PayrollProjectId, saved.ProjectId);
        Assert.Equal(OtherEmployeeId, saved.AssignedToUserId);
        Assert.Equal(AdministratorId, saved.CreatedByUserId);
        Assert.False(saved.IsCompleted);
        VerifyEvent(BusinessEventType.TaskCreated, PayrollProjectId, saved.Id);
        VerifyEvent(BusinessEventType.TaskAssigned, PayrollProjectId, saved.Id);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që administratori mund të ndryshojë projektin dhe caktimin duke ruajtur historikun e krijimit.
    [Fact]
    public async Task UpdateTask_AdministratorMovesAndReassignsTask_PreservesCreationHistory()
    {
        var original = await SeedTaskAsync();
        var request = UpdateRequest(original);
        request.ProjectId = PayrollProjectId;
        request.AssignedToUserId = OtherEmployeeId;

        var result = await Service.UpdateTaskAsync(original.Id, request, AdministratorId, isAdministrator: true);

        Assert.Equal(TaskOperationStatus.Success, result.Status);
        var saved = await ReadTaskAsync(original.Id);
        Assert.Equal(request.Title, saved.Title);
        Assert.Equal(PayrollProjectId, saved.ProjectId);
        Assert.Equal(OtherEmployeeId, saved.AssignedToUserId);
        Assert.Equal(original.CreatedAt, saved.CreatedAt);
        Assert.Equal(original.CreatedByUserId, saved.CreatedByUserId);
        VerifyEvent(BusinessEventType.TaskUpdated, PayrollProjectId, original.Id);
        VerifyEvent(BusinessEventType.TaskAssigned, PayrollProjectId, original.Id);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që edhe administratorit i refuzohet caktimi te një person jashtë projektit të ri.
    [Fact]
    public async Task UpdateTask_AdministratorAssignsOutsideDestinationProject_ReturnsInvalid()
    {
        var original = await SeedTaskAsync();
        var request = UpdateRequest(original);
        request.ProjectId = PayrollProjectId;
        request.AssignedToUserId = TeammateId;

        var result = await Service.UpdateTaskAsync(original.Id, request, AdministratorId, isAdministrator: true);

        await AssertUpdateRejectedAsync(result, TaskOperationStatus.Invalid, original);
    }

    // Kontrollon që administratori mund të përfundojë detyrën e tjetrit edhe pa anëtarësi në projekt.
    [Fact]
    public async Task CompleteTask_AdministratorIsNeitherAssigneeNorProjectMember_CompletesTask()
    {
        var original = await SeedTaskAsync(OtherEmployeeId, PayrollProjectId);

        var result = await Service.CompleteTaskAsync(original.Id, AdministratorId, isAdministrator: true);

        Assert.Equal(TaskOperationStatus.Success, result.Status);
        var saved = await ReadTaskAsync(original.Id);
        Assert.True(saved.IsCompleted);
        Assert.NotNull(saved.CompletedAt);
        Assert.Equal(OtherEmployeeId, saved.AssignedToUserId);
        VerifyEvent(BusinessEventType.TaskCompleted, PayrollProjectId, original.Id);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që rihapja nga administratori heq gjendjen dhe datën e përfundimit.
    [Fact]
    public async Task UpdateTask_AdministratorReopensCompletedTask_ClearsCompletionTime()
    {
        var original = await SeedTaskAsync(completed: true);
        var request = UpdateRequest(original);
        request.IsCompleted = false;

        var result = await Service.UpdateTaskAsync(original.Id, request, AdministratorId, isAdministrator: true);

        Assert.Equal(TaskOperationStatus.Success, result.Status);
        var saved = await ReadTaskAsync(original.Id);
        Assert.False(saved.IsCompleted);
        Assert.Null(saved.CompletedAt);
        VerifyEvent(BusinessEventType.TaskUpdated, PortalProjectId, original.Id);
        Publisher.VerifyNoOtherCalls();
    }

    // Kontrollon që fshirja nga administratori heq vetëm detyrën e zgjedhur dhe ruan projektin.
    [Fact]
    public async Task DeleteTask_AdministratorOperation_DeletesOnlySelectedTask()
    {
        var selected = await SeedTaskAsync();
        var retained = await SeedTaskAsync(TeammateId);

        var result = await Service.DeleteTaskAsync(selected.Id);

        Assert.Equal(TaskOperationStatus.Success, result.Status);
        await using var verificationContext = CreateReadContext();
        Assert.False(await verificationContext.EmployeeTasks.AnyAsync(task => task.Id == selected.Id));
        Assert.True(await verificationContext.EmployeeTasks.AnyAsync(task => task.Id == retained.Id));
        Assert.True(await verificationContext.Projects.AnyAsync(project => project.Id == PortalProjectId));
        VerifyEvent(BusinessEventType.TaskDeleted, PortalProjectId, selected.Id);
        Publisher.VerifyNoOtherCalls();
    }

    private static CreateTaskDto CreateRequest(int projectId, string? assigneeId = null)
        => new()
        {
            Title = "Review leave approval workflow",
            Description = "Verify manager approval and employee notifications.",
            ProjectId = projectId,
            AssignedToUserId = assigneeId
        };

    private static UpdateTaskDto UpdateRequest(EmployeeTask original)
        => new()
        {
            Title = "Handle carried-over leave balances",
            Description = "Include the previous year's remaining allowance.",
            ProjectId = original.ProjectId,
            AssignedToUserId = original.AssignedToUserId
        };

    private async Task AssertCreationRejectedAsync(TaskOperationResult result, TaskOperationStatus expectedStatus)
    {
        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Task);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        await using var verificationContext = CreateReadContext();
        Assert.Empty(await verificationContext.EmployeeTasks.ToListAsync());
        Publisher.VerifyNoOtherCalls();
    }

    private async Task AssertUpdateRejectedAsync(TaskOperationResult result, TaskOperationStatus expectedStatus, EmployeeTask original)
    {
        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Task);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        var saved = await ReadTaskAsync(original.Id);
        Assert.Equal(
            (original.Title, original.Description, original.ProjectId, original.AssignedToUserId,
                original.IsCompleted, original.CompletedAt, original.CreatedByUserId, original.CreatedAt),
            (saved.Title, saved.Description, saved.ProjectId, saved.AssignedToUserId,
                saved.IsCompleted, saved.CompletedAt, saved.CreatedByUserId, saved.CreatedAt));
        Publisher.VerifyNoOtherCalls();
    }
}
