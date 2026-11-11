# Employee Management

## Prerequisites

- Windows with Visual Studio 2022 and the **ASP.NET and web development** workload.
- .NET 8 SDK and Git.
- SQL Server (Express is sufficient) and SQL Server Management Studio 19 or later.
- Node.js 22.12.0 or later, with npm, for the React/Vite application.

## Installation

Clone the repository and enter its root directory:

```powershell
git clone https://github.com/anteakoxherri/EmployeeManagement.git
cd EmployeeManagement
dotnet restore EmployeeManagement.sln
```

Open `EmployeeManagement.sln` in Visual Studio and set `EmployeeManagement.Server` as the startup project. The React application is in `EmployeeManagement.Client` and starts separately.

## Database Setup

1. Start your SQL Server instance and confirm you can connect through SSMS.
2. Check `ConnectionStrings:DefaultConnection` in `EmployeeManagement.Server/appsettings.json`. The default uses `localhost\SQLEXPRESS`, the database `EmployeeManagementDb`, and Windows authentication. Change the server name if your instance differs. Your Windows account must have permission to create/update the database.
3. Complete **Configuration** below before applying migrations.
4. In Visual Studio, open **Tools > NuGet Package Manager > Package Manager Console** and run:

```powershell
Update-Database -Project EmployeeManagement.Server -StartupProject EmployeeManagement.Server -Context ApplicationDbContext
```

EF Core creates the database and applies the existing migrations. Migrations are **not applied automatically** when the API starts. No global `dotnet-ef` installation is required for the Visual Studio command above.

## Configuration

The existing `EmployeeManagement.Server/appsettings.json` defines these non-secret JWT values:

| Setting | Value |
| --- | --- |
| `Jwt:Issuer` | `EmployeeManagementAPI` |
| `Jwt:Audience` | `EmployeeManagementClient` |

In Visual Studio, right-click **EmployeeManagement.Server > Manage User Secrets**. Add the following keys to the local JSON file, replacing every placeholder with your own value:

```json
{
  "Jwt:Key": "<locally generated random secret>",
  "SeedAdmin:Email": "<your administrator email>",
  "SeedAdmin:Password": "<your administrator password>"
}
```

- `Jwt:Key` must contain at least **32 UTF-8 bytes**.
- The administrator email must be valid. The password must contain at least six characters, including an uppercase letter, a lowercase letter, a digit, and a symbol.
- To override the database connection locally, add `ConnectionStrings:DefaultConnection` to the same secrets file.

Keep secrets outside the repository. The project already has a User Secrets ID; no initialization command is needed. The `https` launch profile enables the Development environment, which loads User Secrets and Swagger. Startup requires these settings and creates the `Employee`/`Administrator` roles and initial administrator if missing.

## Running the Project

After configuration and database migration, open a terminal at the repository root:

```powershell
dotnet dev-certs https --trust
dotnet build EmployeeManagement.sln
dotnet run --project EmployeeManagement.Server/EmployeeManagement.Server.csproj --launch-profile https
```

Approve the local development certificate prompt if shown. The API listens at `https://localhost:7199` and `http://localhost:5147`; keep this terminal running.

Open a second terminal at the repository root:

```powershell
cd EmployeeManagement.Client
npm.cmd ci
npm.cmd run dev
```

`npm.cmd ci` installs the locked dependencies and is needed on initial setup or after dependency changes. Open the URL printed by Vite, normally `http://localhost:5173`.

**Current limitation:** the React application is still the starter template and is not connected to the API. Use Swagger to interact with the implemented API endpoints.

## Swagger

1. With the API running in Development, open [Swagger UI](https://localhost:7199/swagger/index.html).
2. Expand `POST /api/auth/login`, select **Try it out**, and enter the administrator email/password configured in User Secrets. Execute the request and copy the returned `token`.
3. Select **Authorize**, paste only the token (without the `Bearer` prefix), and confirm authorization.
4. Test `GET /api/users` and use a returned ID with `GET /api/users/{id}`. Expect `200`; a nonexistent ID returns `404`.
5. To create a user, execute `POST /api/users` with `firstName`, `lastName`, a unique `email`, a password meeting the requirements above, and `role` set to `Employee` or `Administrator`. Expect `201`, `400` for invalid input, or `409` for a duplicate email.
6. `GET /api/employees` returns only Employee-role users; an empty array is valid. These user-management endpoints require an Administrator JWT: no token returns `401`, and an Employee-only token returns `403`.
