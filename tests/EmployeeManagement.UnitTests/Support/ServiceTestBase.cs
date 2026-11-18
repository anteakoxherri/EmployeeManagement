using EmployeeManagement.Models;
using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.Events;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EmployeeManagement.UnitTests.Support;

// xUnit creates a new instance per test case, including each theory row.
// SQLite supports the services' real transactions and ExecuteDeleteAsync; EF's
// non-relational InMemory provider does not. No application host or broker starts.
public abstract class ServiceTestBase : IAsyncLifetime
{
    protected const string EmployeeId = "employee-elda";
    protected const string TeammateId = "employee-arben";
    protected const string OtherEmployeeId = "employee-mira";
    protected const string AdministratorId = "administrator-dritan";
    protected const int PortalProjectId = 101;
    protected const int ReportingProjectId = 102;
    protected const int PayrollProjectId = 103;

    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private DbContextOptions<ApplicationDbContext> _options = null!;

    protected ApplicationDbContext Context { get; private set; } = null!;
    protected Mock<IRabbitMqPublisher> Publisher { get; } = new(MockBehavior.Strict);

    public virtual async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        Context = CreateReadContext();
        await Context.Database.EnsureCreatedAsync();
        Publisher.Setup(publisher => publisher.Publish(It.IsAny<BusinessEvent>())).Returns(true);

        Context.Users.AddRange(
            User(EmployeeId, "Elda", "Hoxha"),
            User(TeammateId, "Arben", "Dervishi"),
            User(OtherEmployeeId, "Mira", "Kola"),
            User(AdministratorId, "Dritan", "Leka"));
        Context.Roles.AddRange(
            new IdentityRole { Id = "role-employee", Name = "Employee", NormalizedName = "EMPLOYEE" },
            new IdentityRole { Id = "role-administrator", Name = "Administrator", NormalizedName = "ADMINISTRATOR" });
        Context.UserRoles.AddRange(
            new IdentityUserRole<string> { UserId = EmployeeId, RoleId = "role-employee" },
            new IdentityUserRole<string> { UserId = TeammateId, RoleId = "role-employee" },
            new IdentityUserRole<string> { UserId = OtherEmployeeId, RoleId = "role-employee" },
            new IdentityUserRole<string> { UserId = AdministratorId, RoleId = "role-administrator" });
        Context.Projects.AddRange(
            new Project { Id = PortalProjectId, Name = "Employee self-service portal", Description = "Leave requests and employee profiles." },
            new Project { Id = ReportingProjectId, Name = "Management reporting", Description = "Monthly workforce reports." },
            new Project { Id = PayrollProjectId, Name = "Payroll integration", Description = "Payroll exports for finance." });
        Context.ProjectEmployees.AddRange(
            new ProjectEmployee { ProjectId = PortalProjectId, UserId = EmployeeId },
            new ProjectEmployee { ProjectId = PortalProjectId, UserId = TeammateId },
            new ProjectEmployee { ProjectId = ReportingProjectId, UserId = EmployeeId },
            new ProjectEmployee { ProjectId = ReportingProjectId, UserId = OtherEmployeeId },
            new ProjectEmployee { ProjectId = PayrollProjectId, UserId = OtherEmployeeId });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // A fresh context makes persistence assertions independent of EF's tracked objects.
    protected ApplicationDbContext CreateReadContext() => new(_options);

    protected async Task<EmployeeTask> SeedTaskAsync(
        string? assigneeId = EmployeeId,
        int projectId = PortalProjectId,
        bool completed = false)
    {
        var task = new EmployeeTask
        {
            Title = "Implement leave balance calculation",
            Description = "Account for approved leave in the employee dashboard.",
            ProjectId = projectId,
            AssignedToUserId = assigneeId,
            CreatedByUserId = projectId == PayrollProjectId ? OtherEmployeeId : TeammateId,
            CreatedAt = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted = completed,
            CompletedAt = completed ? new DateTime(2026, 10, 14, 14, 30, 0, DateTimeKind.Utc) : null
        };
        Context.EmployeeTasks.Add(task);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return task;
    }

    protected async Task<EmployeeTask> ReadTaskAsync(int id)
    {
        await using var verificationContext = CreateReadContext();
        return await verificationContext.EmployeeTasks.AsNoTracking().SingleAsync(task => task.Id == id);
    }

    protected void VerifyEvent(BusinessEventType type, int projectId, int? taskId = null)
        => Publisher.Verify(publisher => publisher.Publish(It.Is<BusinessEvent>(businessEvent =>
            businessEvent.EventType == type && businessEvent.ProjectId == projectId && businessEvent.TaskId == taskId)), Times.Once);

    public virtual async Task DisposeAsync()
    {
        if (Context is not null)
            await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static ApplicationUser User(string id, string firstName, string lastName)
        => new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            UserName = $"{firstName.ToLowerInvariant()}@kreatx.example",
            NormalizedUserName = $"{firstName.ToUpperInvariant()}@KREATX.EXAMPLE",
            Email = $"{firstName.ToLowerInvariant()}@kreatx.example",
            NormalizedEmail = $"{firstName.ToUpperInvariant()}@KREATX.EXAMPLE"
        };
}
