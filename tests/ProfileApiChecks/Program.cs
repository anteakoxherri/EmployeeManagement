using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using EmployeeManagement.Server.Data;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

// Test integrimi me serverin real, JWT/Identity dhe nje databaze SQL te perkohshme te izoluar.
// Nuk lexon kredencialet dhe nuk ndryshon databazen ekzistuese te aplikacionit.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var runId = Guid.NewGuid().ToString("N");
var contentRoot = Path.Combine(root, "tests", "ProfileApiChecks", "artifacts", runId);
Directory.CreateDirectory(contentRoot);
var connection = $"Server=localhost\\SQLEXPRESS;Database=ProfileChecks_{runId};Trusted_Connection=True;TrustServerCertificate=True;";
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
await using var db = new ApplicationDbContext(options);
Process? server = null;
var log = new StringBuilder();
int checks = 0;
try
{
    await db.Database.MigrateAsync();
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var serverDll = Path.Combine(root, "EmployeeManagement.Server/bin", args.FirstOrDefault() ?? "Debug", "net8.0/EmployeeManagement.Server.dll");
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = contentRoot, UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    start.ArgumentList.Add(serverDll);
    start.ArgumentList.Add("--contentRoot");
    start.ArgumentList.Add(contentRoot);
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    start.Environment["ConnectionStrings__DefaultConnection"] = connection;
    start.Environment["Jwt__Key"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    start.Environment["Jwt__Issuer"] = "ProfileChecks";
    start.Environment["Jwt__Audience"] = "ProfileChecks";
    start.Environment["SeedAdmin__Email"] = "admin@profile-checks.test";
    start.Environment["SeedAdmin__Password"] = "ProfileChecks!936";
    server = Process.Start(start)!;
    server.OutputDataReceived += (_, e) => { lock (log) log.AppendLine(e.Data); };
    server.ErrorDataReceived += (_, e) => { lock (log) log.AppendLine(e.Data); };
    server.BeginOutputReadLine();
    server.BeginErrorReadLine();
    using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(15) };
    var ready = false;
    for (var attempt = 0; attempt < 100; attempt++)
    {
        if (server.HasExited) throw new Exception("Server exited: " + log);
        try { using var response = await client.GetAsync("/swagger/v1/swagger.json"); ready = response.IsSuccessStatusCode; }
        catch (HttpRequestException) { }
        if (ready) break;
        await Task.Delay(200);
    }
    Check(ready, "Server started with isolated SQL database");
    var swagger = JsonNode.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"))!;
    Check(swagger["paths"]!["/api/profile/me/picture"]!["post"]!["requestBody"]!["content"]!["multipart/form-data"] != null,
        "Swagger exposes multipart picture upload");
    await Status(await client.GetAsync("/api/profile/me"), HttpStatusCode.Unauthorized, "GET requires JWT");
    await Status(await client.PutAsJsonAsync("/api/profile/me", Profile("Anonymous")), HttpStatusCode.Unauthorized, "PUT requires JWT");
    await Status(await Upload([1, 2, 3], "x.png"), HttpStatusCode.Unauthorized, "Upload requires JWT");
    var adminToken = await Login("admin@profile-checks.test");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    var created = await client.PostAsJsonAsync("/api/users", new
    {
        firstName = "Employee", lastName = "Check", email = "employee@profile-checks.test", password = "ProfileChecks!936", role = "Employee"
    });
    await Status(created, HttpStatusCode.Created, "Create isolated employee through existing API");
    var employeeId = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
    var employeeToken = await Login("employee@profile-checks.test");

    foreach (var (token, role, email) in new[]
    {
        (adminToken, "Administrator", "admin@profile-checks.test"),
        (employeeToken, "Employee", "employee@profile-checks.test")
    })
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var before = await ReadProfile();
        Check(before["role"]!.GetValue<string>() == role && before["email"]!.GetValue<string>() == email, $"{role}: GET returns own identity");
        var updated = await client.PutAsJsonAsync("/api/profile/me", Profile(role + " Updated"));
        await Status(updated, HttpStatusCode.OK, $"{role}: PUT succeeds");
        var after = await ReadProfile();
        Check(after["firstName"]!.GetValue<string>() == role + " Updated" && after["phoneNumber"]!.GetValue<string>() == "+355 69 123 4567", $"{role}: changes persisted");
        foreach (var forbidden in new[] { "email", "role", "password", "id", "profilePicturePath" })
        {
            var body = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(Profile("Overpost")))!.AsObject();
            body[forbidden] = forbidden == "role" ? "Administrator" : "forbidden";
            await Status(await client.PutAsJsonAsync("/api/profile/me", body), HttpStatusCode.BadRequest, $"{role}: rejects {forbidden}");
        }
        await Status(await client.PutAsJsonAsync("/api/profile/me", Profile(" ")), HttpStatusCode.BadRequest, "Reject whitespace name");
        await Status(await client.PutAsJsonAsync("/api/profile/me", new { firstName = "Valid", lastName = "Check", phoneNumber = "invalid" }), HttpStatusCode.BadRequest, "Validate phone");
        await Status(await client.PutAsJsonAsync("/api/profile/me", Profile(new string('a', 101))), HttpStatusCode.BadRequest, "Validate name length");
        var unchanged = await ReadProfile();
        Check(unchanged["firstName"]!.GetValue<string>() == role + " Updated" && unchanged["email"]!.GetValue<string>() == email && unchanged["role"]!.GetValue<string>() == role, "Rejected changes leave profile unchanged");
        var ownUpdate = await client.PutAsJsonAsync("/api/profile/me?userId=" + employeeId, Profile(role + " Own"));
        await Status(ownUpdate, HttpStatusCode.OK, "Query ID cannot choose update target");

        var png = ImageBytes(SKEncodedImageFormat.Png);
        var jpg = ImageBytes(SKEncodedImageFormat.Jpeg);
        await Status(await Upload(Encoding.UTF8.GetBytes("not an image"), "fake.png"), HttpStatusCode.BadRequest, "Reject fake PNG");
        await Status(await Upload(png[..24], "broken.png"), HttpStatusCode.BadRequest, "Reject truncated PNG");
        await Status(await Upload(png, "mismatch.jpg"), HttpStatusCode.BadRequest, "Reject mismatched content");
        await Status(await Upload(png, "picture.gif"), HttpStatusCode.BadRequest, "Reject extension");
        await Status(await Upload([], "empty.png"), HttpStatusCode.BadRequest, "Reject empty file");
        await Status(await Upload(new byte[2 * 1024 * 1024 + 1], "large.png"), HttpStatusCode.BadRequest, "Reject more than 2 MB");
        await Status(await Upload(png, "../../untrusted.PNG"), HttpStatusCode.OK, $"{role}: PNG upload succeeds with safe generated name");
        var oldPath = (await ReadProfile())["profilePicturePath"]!.GetValue<string>();
        Check(oldPath.StartsWith("/uploads/profile-pictures/") && !oldPath.Contains("untrusted"), "Relative generated path");
        using var publicClient = new HttpClient { BaseAddress = client.BaseAddress };
        var imageResponse = await publicClient.GetAsync(oldPath);
        await Status(imageResponse, HttpStatusCode.OK, "Picture accessible without JWT");
        Check(imageResponse.Content.Headers.ContentType?.MediaType == "image/png", "Picture content type");
        Check(imageResponse.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "Picture nosniff header");
        await Status(await Upload(jpg, "replacement.JPEG"), HttpStatusCode.OK, $"{role}: JPEG replacement succeeds");
        var newPath = (await ReadProfile())["profilePicturePath"]!.GetValue<string>();
        Check(newPath != oldPath, "Replacement uses unique name");
        await Status(await publicClient.GetAsync(oldPath), HttpStatusCode.NotFound, "Replaced file removed");
        await Status(await publicClient.GetAsync(newPath), HttpStatusCode.OK, "Replacement accessible");
        await Status(await Upload([1, 2], "bad.jpg"), HttpStatusCode.BadRequest, "Invalid replacement rejected");
        Check((await ReadProfile())["profilePicturePath"]!.GetValue<string>() == newPath, "Invalid replacement preserves existing picture");
        await Status(await publicClient.GetAsync("/uploads/profile-pictures/"), HttpStatusCode.NotFound, "Directory listing disabled");
        await Status(await publicClient.GetAsync("/uploads/profile-pictures/%2e%2e/appsettings.json"), HttpStatusCode.NotFound, "Traversal cannot expose configuration");
    }
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", employeeToken);
    Check((await ReadProfile())["firstName"]!.GetValue<string>() == "Employee Own", "Employee keeps own changes");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    Check((await ReadProfile())["firstName"]!.GetValue<string>() == "Administrator Own", "Employee changes never modify administrator");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", employeeToken + "tampered");
    await Status(await client.GetAsync("/api/profile/me"), HttpStatusCode.Unauthorized, "Tampered JWT rejected");
    Console.WriteLine($"PASS: {checks} integration checks.");

    object Profile(string name) => new { firstName = name, lastName = "Check", phoneNumber = "+355 69 123 4567" };
    async Task<string> Login(string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "ProfileChecks!936" });
        await Status(response, HttpStatusCode.OK, "Login with real JWT");
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
    }
    async Task<JsonObject> ReadProfile()
    {
        using var response = await client.GetAsync("/api/profile/me");
        await Status(response, HttpStatusCode.OK, "GET own profile");
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }
    async Task<HttpResponseMessage> Upload(byte[] bytes, string filename)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", filename);
        return await client.PostAsync("/api/profile/me/picture", form);
    }
}
finally
{
    if (server is { HasExited: false }) { server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
    server?.Dispose();
    // Emri unik eshte krijuar vetem nga ky test; fshirja nuk prek EmployeeManagementDb.
    await db.Database.EnsureDeletedAsync();
    Directory.Delete(contentRoot, recursive: true);
}

void Check(bool success, string description)
{
    if (!success) throw new Exception("FAIL: " + description);
    checks++;
    Console.WriteLine("PASS: " + description);
}
async Task Status(HttpResponseMessage response, HttpStatusCode expected, string description)
{
    Check(response.StatusCode == expected, $"{description} ({(int)response.StatusCode}, expected {(int)expected})" +
        (response.StatusCode == expected ? "" : ": " + await response.Content.ReadAsStringAsync()));
}
static byte[] ImageBytes(SKEncodedImageFormat format)
{
    using var bitmap = new SKBitmap(8, 8);
    bitmap.Erase(SKColors.CornflowerBlue);
    using var image = SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(format, 90);
    return encoded.ToArray();
}
