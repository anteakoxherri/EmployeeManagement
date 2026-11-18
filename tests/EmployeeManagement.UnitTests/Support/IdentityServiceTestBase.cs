using EmployeeManagement.Models;
using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EmployeeManagement.UnitTests.Support;

public abstract class IdentityServiceTestBase : ServiceTestBase
{
    private ServiceProvider _identityServices = null!;
    protected Mock<UserManager<ApplicationUser>> Manager { get; private set; } = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _identityServices = new ServiceCollection().BuildServiceProvider();

        // Identity uses its real validators, normalizer, password hasher and EF store.
        // CallBase lets individual tests override a failure without faking persistence.
        Manager = new Mock<UserManager<ApplicationUser>>(
            new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(Context),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new IUserValidator<ApplicationUser>[] { new UserValidator<ApplicationUser>() },
            new IPasswordValidator<ApplicationUser>[] { new PasswordValidator<ApplicationUser>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            _identityServices,
            NullLogger<UserManager<ApplicationUser>>.Instance)
        { CallBase = true };
    }

    protected async Task<ApplicationUser> ReadUserAsync(string id)
    {
        await using var verificationContext = CreateReadContext();
        return await verificationContext.Users.AsNoTracking().SingleAsync(user => user.Id == id);
    }

    protected async Task<string[]> ReadRolesAsync(string id)
    {
        await using var verificationContext = CreateReadContext();
        return await (from membership in verificationContext.UserRoles
                      join role in verificationContext.Roles on membership.RoleId equals role.Id
                      where membership.UserId == id
                      orderby role.Name
                      select role.Name!).ToArrayAsync();
    }

    protected void VerifyUserEvent(BusinessEventType type, string userId)
    {
        Publisher.Verify(publisher => publisher.Publish(It.Is<BusinessEvent>(businessEvent =>
            businessEvent.EventType == type && businessEvent.UserId == userId && businessEvent.EntityId == userId)), Times.Once);
        Publisher.VerifyNoOtherCalls();
    }

    public override async Task DisposeAsync()
    {
        if (Manager is not null)
            Manager.Object.Dispose();
        if (_identityServices is not null)
            await _identityServices.DisposeAsync();
        await base.DisposeAsync();
    }
}
