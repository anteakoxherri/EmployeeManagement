
using Microsoft.OpenApi.Models;
using EmployeeManagement.Server.Data;
using EmployeeManagement.Models;
using EmployeeManagement.Server.Services;
using EmployeeManagement.Server.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Pika e nisjes se API-se: builder mbledh konfigurimin dhe regjistron varesite ne DI.
// Konfigurimi mund te vije nga appsettings, User Secrets ne Development dhe variablat e ambientit.
var builder = WebApplication.CreateBuilder(args);

// Regjistrojme DbContext me provider-in SQL Server. Connection string merret nga konfigurimi.
// Code-First do te thote se modelet/konfigurimi C# dhe migrations pershkruajne skemen e databazes.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity menaxhon perdoruesit, hash-et e password-eve dhe rolet permes UserManager/RoleManager.
// EntityFrameworkStores i lidh keto sherbime me tabelat e ApplicationDbContext.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// DI lidh cdo interface me service-in qe e zbaton. Controllers kerkojne interface ne konstruktor.
// Scoped krijon nje instance per kerkese HTTP; sherbimet ne ate kerkese ndajne te njejtin DbContext.
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWeatherForecastService, WeatherForecastService>();

// Issuer tregon kush e leshon token-in, Audience tregon per ke vlen, Key eshte sekreti i nenshkrimit.
// Key nuk duhet publikuar ose vendosur ne repository; merret nga konfigurimi i sigurt.
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

// Kontrollojme nese konfigurimet e JWT jane vendosur sakte
if (string.IsNullOrWhiteSpace(jwtKey) ||
    string.IsNullOrWhiteSpace(jwtIssuer) ||
    string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "JWT configuration is missing.");
}

// Kontrollojme gjatesine e celesit sekret
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT secret key must be at least 32 bytes.");
}

// Konfigurimi i JWT Authentication
builder.Services.AddAuthentication(options =>
{
    // JWT perdoret si skema kryesore e autentikimit
    options.DefaultScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // API verifikon leshuesin, marresin, skadimin dhe nenshkrimin per cdo token te derguar.
    // Nje token i ndryshuar ose i skaduar refuzohet edhe nese permban tekstin Administrator.
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)),

        // Pa kohe tolerance shtese pas skadimit te token-it.
        ClockSkew = TimeSpan.Zero
    };
});

// Autentikimi identifikon perdoruesin; autorizimi kontrollon nese roli i tij lejohet te endpoint-i.
builder.Services.AddAuthorization();

// Regjistrojme controllers dhe zbulimin e endpoints qe Swagger te gjeneroje dokumentacionin.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Konfigurimi i Swagger per JWT
builder.Services.AddSwaggerGen(options =>
{
    // Kjo shton butonin Authorize ne Swagger; aty vendoset token-i, pa shkruar perseri Bearer.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Vendos JWT token-in."
    });

    // Lejojme Swagger te dergoje JWT ne kerkesat API
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Ndertojme aplikacionin pasi sherbimet jane regjistruar; me poshte percaktojme rrugen e kerkesave.
var app = builder.Build();

// Nisja perdor nje scope sepse DbContext dhe sherbimet Identity kane jetegjatesi Scoped.
// Databaza duhet t'i kete migrations te aplikuara para se seeder te kerkoje tabelat.
using (var scope = app.Services.CreateScope())
{
    // Marrim sherbimet dhe konfigurimin e aplikacionit
    var services = scope.ServiceProvider;
    var configuration = services.GetRequiredService<IConfiguration>();

    // Ekzekutojme DbSeeder per rolet dhe administratorin
    await DbSeeder.SeedAsync(services, configuration);
}

// Aktivizimi i Swagger ne Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware jane hapa qe perpunojne kerkesen me radhe perpara se ajo te arrije te controller-i.
// HTTPS redirection e drejton klientin drejt lidhjes HTTPS kur ajo eshte konfiguruar.
app.UseHttpsRedirection();

// Rendi ka rendesi: fillimisht lexohet/verifikohet JWT dhe ndertohet HttpContext.User.
app.UseAuthentication();

// Pastaj kontrollohen [Authorize] dhe rolet. Pa identitet merret 401; pa lejen e duhur merret 403.
app.UseAuthorization();

// MapControllers aktivizon routes e percaktuara me [Route], [HttpGet] dhe [HttpPost].
app.MapControllers();

// Nis serverin dhe e mban ne pritje te kerkesave HTTP.
app.Run();
