using System.Security.Claims;
using System.Text.Encodings.Web;
using EmployeeManagement.Models;
using EmployeeManagement.Server.Controllers;
using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.Interfaces;
using EmployeeManagement.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmployeeManagement.UnitTests.Support;

public sealed class AccountApiHost : IDisposable
{
    private readonly IHost _host;
    public HttpClient Client { get; }

    private AccountApiHost(IHost host)
    {
        _host = host;
        Client = host.GetTestClient();
    }

    public static async Task<AccountApiHost> StartAsync(
        ApplicationDbContext context, UserManager<ApplicationUser> manager,
        IRabbitMqPublisher publisher, string contentRoot)
    {
        // Load the actual controllers, MVC validation and authorization without
        // executing production Program.cs, its SQL configuration or hosted broker services.
        var host = await new HostBuilder().ConfigureWebHost(web => web
            .UseTestServer()
            .UseContentRoot(contentRoot)
            .ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.ClearProviders());
                services.AddSingleton(context);
                services.AddSingleton(manager);
                services.AddSingleton(publisher);
                services.AddScoped<IUserService, UserService>();
                services.AddScoped<IProfileService, ProfileService>();
                services.AddControllers().AddApplicationPart(typeof(ProfileController).Assembly);
                services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
                services.AddAuthorization();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            })).StartAsync();
        return new AccountApiHost(host);
    }

    public void SignIn(string id, string role)
    {
        Client.DefaultRequestHeaders.Remove("X-Test-User");
        Client.DefaultRequestHeaders.Remove("X-Test-Role");
        Client.DefaultRequestHeaders.Add("X-Test-User", id);
        Client.DefaultRequestHeaders.Add("X-Test-Role", role);
    }

    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();
    }
}

// Test-only authentication supplies trusted claims; it does not validate JWTs.
// Requests still pass through the real authorization and MVC filters.
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AccountTests";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var id = Request.Headers["X-Test-User"].ToString();
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())
        }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
