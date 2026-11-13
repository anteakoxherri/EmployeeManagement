using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EmployeeManagement.Server.Data;
using EmployeeManagement.Server.Events;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using SkiaSharp;

// Prova me HTTP, SQL Server dhe nje broker Docker te izoluar; nuk prek broker-in/DB ekzistuese.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var id = Guid.NewGuid().ToString("N");
var artifactsRoot = Path.Combine(root, "tests/RabbitMqChecks/artifacts");
var contentRoot = Path.Combine(artifactsRoot, id);
Directory.CreateDirectory(contentRoot);
var containerName = "employee-events-checks-" + id;
var rabbitUser = "checks-" + id;
var rabbitPassword = Guid.NewGuid().ToString("N");
var queue = "checks.business-events." + id;
var sql = $"Server=localhost\\SQLEXPRESS;Database=RabbitChecks_{id};Trusted_Connection=True;TrustServerCertificate=True;";
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(sql).Options);
Process? server = null;
var logs = new StringBuilder();
var containerCreated = false;
var databaseCreated = false;
var count = 0;
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
try
{
    await Docker("run", "-d", "--name", containerName, "-p", "127.0.0.1::5672",
        "--env", "RABBITMQ_DEFAULT_USER", "--env", "RABBITMQ_DEFAULT_PASS", "rabbitmq:4-management");
    containerCreated = true;
    var mapping = await Docker("port", containerName, "5672");
    var rabbitPort = int.Parse(mapping.Trim().Split(':').Last());
    var factory = new ConnectionFactory
    {
        HostName = "127.0.0.1", Port = rabbitPort, UserName = rabbitUser, Password = rabbitPassword,
        AutomaticRecoveryEnabled = false, RequestedConnectionTimeout = TimeSpan.FromSeconds(2)
    };
    IConnection? observer = null;
    await WaitUntil(async () =>
    {
        try { observer = await factory.CreateConnectionAsync(); return true; }
        catch { return false; }
    }, "Isolated RabbitMQ broker ready", 60);
    await using (observer!)
    await using (var channel = await observer!.CreateChannelAsync())
    {
        await db.Database.MigrateAsync();
        databaseCreated = true;
        await StartServer(consumerEnabled: false, rabbitPort);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@events.test", password = "EventChecks!936" });
        await Status(login, 200, "Existing login works");
        var token = (await login.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var userResponse = await client.PostAsJsonAsync("/api/users", new
        {
            firstName = "Check", lastName = "Employee", email = "employee@events.test", password = "EventChecks!936", role = "Employee"
        });
        await Status(userResponse, 201, "UserCreated");
        var userId = (await userResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
        await Status(await client.PutAsJsonAsync("/api/users/" + userId, new
        {
            firstName = "Updated", lastName = "Employee", email = "employee@events.test", role = "Employee"
        }), 200, "UserUpdated");
        await Status(await client.PutAsJsonAsync("/api/profile/me", new
        {
            firstName = "Admin", lastName = "Updated", phoneNumber = "+355 69 123 4567"
        }), 200, "ProfileUpdated");
        using (var bitmap = new SKBitmap(8, 8))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var bytes = image.Encode(SKEncodedImageFormat.Png, 90))
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new ByteArrayContent(bytes.ToArray()), "file", "profile.png");
            await Status(await client.PostAsync("/api/profile/me/picture", form), 200, "ProfilePictureUpdated");
        }
        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Event project", description = "Private text" });
        await Status(projectResponse, 201, "ProjectCreated");
        var projectId = (await projectResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
        await Status(await client.PutAsJsonAsync($"/api/projects/{projectId}", new { name = "Updated project", description = "Private text" }), 200, "ProjectUpdated");
        await Status(await client.PostAsync($"/api/projects/{projectId}/employees/{userId}", null), 201, "EmployeeAddedToProject");
        await Status(await client.PostAsync($"/api/projects/{projectId}/employees/{userId}", null), 409, "Duplicate membership remains 409");
        var taskResponse = await client.PostAsJsonAsync("/api/tasks", new { title = "Private task title", projectId });
        await Status(taskResponse, 201, "TaskCreated");
        var taskId = (await taskResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
        await Status(await client.PutAsJsonAsync($"/api/tasks/{taskId}", new { title = "Updated task", projectId, assignedToUserId = userId }), 200, "TaskUpdated and TaskAssigned");
        await Status(await client.DeleteAsync($"/api/projects/{projectId}"), 409, "Incomplete task still blocks project deletion");
        await Status(await client.DeleteAsync($"/api/projects/{projectId}/employees/{userId}"), 409, "Assigned task still blocks membership removal");
        await Status(await client.PatchAsync($"/api/tasks/{taskId}/complete", null), 200, "TaskCompleted");
        await Status(await client.PatchAsync($"/api/tasks/{taskId}/complete", null), 200, "Repeated completion remains successful without duplicate event");
        await Status(await client.DeleteAsync($"/api/tasks/{taskId}"), 204, "TaskDeleted");
        await Status(await client.DeleteAsync($"/api/projects/{projectId}/employees/{userId}"), 204, "EmployeeRemovedFromProject");
        await Status(await client.DeleteAsync($"/api/projects/{projectId}"), 204, "ProjectDeleted");
        await Status(await client.DeleteAsync($"/api/users/{userId}"), 204, "UserDeleted");
        await Status(await client.GetAsync("/api/users/" + userId), 404, "Deleted entity remains 404");
        await WaitUntil(async () => (await channel.QueueDeclarePassiveAsync(queue)).MessageCount == 15,
            "All 15 events stored in RabbitMQ before consumer starts");

        var events = new List<BusinessEvent>();
        ulong lastTag = 0;
        for (var i = 0; i < 15; i++)
        {
            var message = await channel.BasicGetAsync(queue, autoAck: false) ?? throw new Exception("Missing event");
            lastTag = message.DeliveryTag;
            var json = Encoding.UTF8.GetString(message.Body.Span);
            var businessEvent = JsonSerializer.Deserialize<BusinessEvent>(json)!;
            events.Add(businessEvent);
            Check(message.BasicProperties.Persistent, businessEvent.EventType + " is persistent");
            Check(businessEvent.Timestamp.Offset == TimeSpan.Zero && businessEvent.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-5), "UTC event time");
            Check(businessEvent.EventId != Guid.Empty && !string.IsNullOrEmpty(businessEvent.EntityId), "Event ID and entity ID");
            Check(!json.Contains('@') && !json.Contains("Private") && !json.Contains("phone", StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("password", StringComparison.OrdinalIgnoreCase) && !json.Contains("token", StringComparison.OrdinalIgnoreCase), "No personal data or credentials in payload");
        }
        Check(events.Select(e => e.EventId).Distinct().Count() == 15, "All event IDs unique");
        Check(Enum.GetValues<BusinessEventType>().All(type => events.Count(e => e.EventType == type) == 1), "Exactly one of each supported event; rejected operations emit none");
        Check(events.Where(e => e.TaskId != null).All(e => e.TaskId == taskId && e.ProjectId == projectId), "Task contextual IDs match");
        Check(events.Single(e => e.EventType == BusinessEventType.TaskAssigned).AssignedToUserId == userId, "Assignment includes employee ID");
        Check(events.Single(e => e.EventType == BusinessEventType.UserCreated).UserId == userId, "Registration event identifies created user");
        await channel.BasicNackAsync(lastTag, multiple: true, requeue: true);
        await StopServer();
        await StartServer(consumerEnabled: true, rabbitPort);
        await WaitUntil(() => Task.FromResult(events.All(e => LogText().Contains("Event i konsumuar: " + e.EventType + ", EventId " + e.EventId))),
            "Real BackgroundService consumed and logged all 15 event IDs");
        await WaitUntil(async () => (await channel.QueueDeclarePassiveAsync(queue)).MessageCount == 0,
            "Consumer acknowledged queued events");

        var confirmedBefore = Occurrences(LogText(), "RabbitMQ konfirmoi publikimin:");
        await Docker("stop", "--time", "2", containerName);
        var timer = Stopwatch.StartNew();
        await Status(await client.PutAsJsonAsync("/api/profile/me", new { firstName = "Offline", lastName = "Saved" }), 200,
            "Successful DB operation survives broker outage");
        Check(timer.Elapsed < TimeSpan.FromSeconds(2), "API does not wait for broker connection attempts");
        var profile = await client.GetFromJsonAsync<JsonObject>("/api/profile/me");
        Check(profile!["firstName"]!.GetValue<string>() == "Offline", "DB update persisted while broker unavailable");
        await WaitUntil(() => Task.FromResult(LogText().Contains("u hoq nga buffer-i pas deshtimeve")), "Failed delivery explicitly logged after bounded retries", 30);
        Check(Occurrences(LogText(), "RabbitMQ konfirmoi publikimin:") == confirmedBefore, "Failed publish is never logged as confirmed");
    }
    Console.WriteLine($"PASS: {count} RabbitMQ integration checks.");
}
finally
{
    await StopServer();
    if (containerCreated) await Docker("rm", "-f", containerName);
    if (databaseCreated) await db.Database.EnsureDeletedAsync();
    var resolved = Path.GetFullPath(contentRoot);
    if (!resolved.StartsWith(Path.GetFullPath(artifactsRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Unsafe cleanup path");
    Directory.Delete(resolved, recursive: true);
}

async Task StartServer(bool consumerEnabled, int rabbitPort)
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    client.BaseAddress ??= new Uri($"http://127.0.0.1:{port}");
    // HttpClient.BaseAddress nuk ndryshohet pas kerkeses se pare; perdorim te njejtin port ne rinisje.
    port = client.BaseAddress.Port;
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = contentRoot, UseShellExecute = false,
        CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(Path.Combine(root, "EmployeeManagement.Server/bin/Debug/net8.0/EmployeeManagement.Server.dll"));
    start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(contentRoot);
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    start.Environment["ConnectionStrings__DefaultConnection"] = sql;
    // JWT key mbetet i njejte ne rinisje, ne menyre qe token-i i testit te vazhdoje te vleje.
    start.Environment["Jwt__Key"] = id + id;
    start.Environment["Jwt__Issuer"] = "EventChecks";
    start.Environment["Jwt__Audience"] = "EventChecks";
    start.Environment["SeedAdmin__Email"] = "admin@events.test";
    start.Environment["SeedAdmin__Password"] = "EventChecks!936";
    start.Environment["RabbitMq__HostName"] = "127.0.0.1";
    start.Environment["RabbitMq__Port"] = rabbitPort.ToString();
    start.Environment["RabbitMq__UserName"] = rabbitUser;
    start.Environment["RabbitMq__Password"] = rabbitPassword;
    start.Environment["RabbitMq__QueueName"] = queue;
    start.Environment["RabbitMq__ConsumerEnabled"] = consumerEnabled.ToString();
    start.Environment["RabbitMq__TimeoutSeconds"] = "2";
    start.Environment["RabbitMq__RetryDelaySeconds"] = "1";
    server = Process.Start(start)!;
    server.OutputDataReceived += (_, e) => { lock (logs) logs.AppendLine(e.Data); };
    server.ErrorDataReceived += (_, e) => { lock (logs) logs.AppendLine(e.Data); };
    server.BeginOutputReadLine(); server.BeginErrorReadLine();
    await WaitUntil(async () =>
    {
        if (server.HasExited) throw new Exception("API exited: " + LogText());
        try { using var response = await client.GetAsync("/swagger/v1/swagger.json"); return response.IsSuccessStatusCode; }
        catch (HttpRequestException) { return false; }
    }, "API started with isolated configuration");
}
async Task StopServer()
{
    if (server is { HasExited: false }) { server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
    server?.Dispose(); server = null;
}
async Task<string> Docker(params string[] arguments)
{
    var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    start.Environment["RABBITMQ_DEFAULT_USER"] = rabbitUser;
    start.Environment["RABBITMQ_DEFAULT_PASS"] = rabbitPassword;
    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception("Docker: " + await error);
    return await output;
}
string LogText() { lock (logs) return logs.ToString(); }
void Check(bool success, string description)
{
    if (!success) throw new Exception("FAIL: " + description);
    count++; Console.WriteLine("PASS: " + description);
}
async Task Status(HttpResponseMessage response, int expected, string description)
{
    Check((int)response.StatusCode == expected, description + $" ({(int)response.StatusCode})" +
        ((int)response.StatusCode == expected ? "" : ": " + await response.Content.ReadAsStringAsync()));
}
async Task WaitUntil(Func<Task<bool>> predicate, string description, int seconds = 30)
{
    var timer = Stopwatch.StartNew();
    while (timer.Elapsed < TimeSpan.FromSeconds(seconds))
    {
        if (await predicate()) { Check(true, description); return; }
        await Task.Delay(200);
    }
    throw new Exception("Timeout: " + description + "\n" + LogText());
}
static int Occurrences(string text, string value) => text.Split(value).Length - 1;
