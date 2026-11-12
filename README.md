# Employee Management

An ASP.NET Core backend for an employee management application. 

## Prerequisites

- Windows with Visual Studio 2022 and the **ASP.NET and web development** workload.
- .NET 8 SDK.
- SQL Server (Express is sufficient).

## Setup and Run

1. Clone or download the repository and open `EmployeeManagement.sln` in Visual Studio.

2. Start SQL Server. The connection in `EmployeeManagement.Server/appsettings.json` uses `localhost\SQLEXPRESS`, the database `EmployeeManagementDb`, and Windows authentication. Your account must have permission to create and update the database.

3. Right-click **EmployeeManagement.Server > Manage User Secrets** and add the following, replacing each placeholder with your own value:

   ```json
   {
     "Jwt:Key": "<random secret of at least 32 UTF-8 bytes>",
     "SeedAdmin:Email": "<your administrator email>",
     "SeedAdmin:Password": "<your administrator password>"
   }
   ```

   Use a valid email and a password of at least six characters containing uppercase and lowercase letters, a digit, and a symbol. If your SQL Server instance differs, also add `ConnectionStrings:DefaultConnection` with your connection string to User Secrets. Keep credentials outside the repository.

4. Open **Tools > NuGet Package Manager > Package Manager Console** and apply the database migrations:

   ```powershell
   Update-Database -Project EmployeeManagement.Server -StartupProject EmployeeManagement.Server -Context ApplicationDbContext
   ```

5. Open a terminal in the repository root and run:

   ```powershell
   dotnet dev-certs https --trust
   dotnet run --project EmployeeManagement.Server/EmployeeManagement.Server.csproj --launch-profile https
   ```

   Accept the development certificate prompt if shown. The backend runs at `https://localhost:7199`. Keep the terminal open; press `Ctrl+C` to stop it. The initial administrator is created automatically on startup using your configured credentials.
