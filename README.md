# Employee Management System

A KreatX internship application built with ASP.NET Core Web API, React, Entity Framework Core, SQL Server, JWT authentication and RabbitMQ.

## Features

- JWT authentication with Administrator and Employee roles.
- User management, personal profiles and profile-picture uploads.
- Project management and employee membership.
- Task creation, assignment, editing and completion with role-based permissions.
- RabbitMQ event publishing and consumption.
- Swagger API documentation.

## Permissions

**Administrators** manage users, projects, memberships and all tasks, including reassignment and deletion. Projects with open tasks cannot be deleted.

**Employees** update their own profiles, view tasks in their projects and create tasks assigned to themselves or another Employee member of the selected project. They can edit the title/description and complete only tasks currently assigned to them. They cannot change an existing task's project or assignee, reopen completed tasks, modify colleagues' tasks or delete tasks.

## Prerequisites

- Visual Studio 2022 with the ASP.NET and web development workload, and .NET 8 SDK/runtime.
- Node.js `^20.19.0` or `>=22.12.0`, with npm.
- SQL Server (SQL Server Express is supported).
- Docker Desktop with Linux containers for RabbitMQ.

## Setup and run

### 1. Open and restore

Clone the repository, open `EmployeeManagement.sln` in Visual Studio and run from the repository root:

```powershell
dotnet restore EmployeeManagement.sln
```

### 2. Configure SQL Server and User Secrets

Start SQL Server. In Visual Studio, right-click **EmployeeManagement.Server > Manage User Secrets** and add the following, replacing placeholders and adjusting the SQL Server instance if needed:

```json
{
  "ConnectionStrings:DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=EmployeeManagementDb;Trusted_Connection=True;TrustServerCertificate=True;",
  "Jwt:Key": "<random secret of at least 32 UTF-8 bytes>",
  "SeedAdmin:Email": "<administrator email>",
  "SeedAdmin:Password": "<administrator password>",
  "RabbitMq:UserName": "<broker username>",
  "RabbitMq:Password": "<broker password>"
}
```

Your Windows account needs database creation/update permissions. Use an Administrator password with at least six characters, including uppercase, lowercase, a digit and a symbol. JWT issuer/audience and local RabbitMQ defaults are configured in `appsettings.json`. Keep credentials outside source control.

### 3. Apply migrations

In Visual Studio's **Tools > NuGet Package Manager > Package Manager Console**, run:

```powershell
Update-Database -Project EmployeeManagement.Server -StartupProject EmployeeManagement.Server -Context ApplicationDbContext
```

### 4. Start RabbitMQ

Create a `.env` file in the repository root with the same broker credentials used in User Secrets:

```dotenv
RABBITMQ_USERNAME=<broker username>
RABBITMQ_PASSWORD=<broker password>
```

With Docker Desktop running, execute from the repository root:

```powershell
docker compose -f docker-compose.rabbitmq.yml up -d
```

Default AMQP port: `5672`. Management UI: `http://localhost:15672`. The `.env` file is ignored by Git.

### 5. Run the backend

From the repository root:

```powershell
dotnet dev-certs https --trust
dotnet run --project EmployeeManagement.Server/EmployeeManagement.Server.csproj --launch-profile https
```

Backend: `https://localhost:7199`. Swagger: `https://localhost:7199/swagger`. The initial Administrator is created on startup from User Secrets; no public registration endpoint is provided.

### 6. Run React

In a second terminal, starting from the repository root:

```powershell
cd EmployeeManagement.Client
npm.cmd ci
npm.cmd run dev
```

Open `http://localhost:5173` and sign in with the configured Administrator account. Keep the backend running; Vite proxies API requests to port `7199`. Use `npm` instead of `npm.cmd` outside Windows.
