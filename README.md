# KreatX Employee Management System

An internship project built with ASP.NET Core (.NET 8), React, Entity Framework Core, SQL Server, ASP.NET Core Identity, JWT and RabbitMQ.

## Features

- Authentication with Administrator and Employee roles.
- User management, personal profiles and profile-picture uploads.
- Projects, employee membership, task assignment and completion.
- RabbitMQ business events, Swagger documentation and service unit tests.

## Permissions

**Administrators** manage users, projects, memberships and tasks. Projects containing open tasks cannot be deleted.

**Employees** manage their profiles, view tasks in their projects and create tasks for themselves or other Employee members of the same project. They can edit and complete their assigned tasks, but cannot reassign or move existing tasks, reopen completed tasks or delete tasks.

## Prerequisites

- **Docker deployment:** Docker Desktop with Linux containers, or Docker Engine with Docker Compose.
- **Local development:** Visual Studio 2022 with the ASP.NET workload, .NET 8 SDK/runtime, Node.js 22.12+ and npm.
- **Local services:** SQL Server (Express supported) and Docker for RabbitMQ.

## Docker Compose

Run commands from the repository root. Open Docker Desktop before starting.

1. Copy the environment template; if `.env` exists, add the missing settings instead of overwriting it:

   ```powershell
   Copy-Item .env.example .env
   ```

2. Fill `MSSQL_SA_PASSWORD`, `JWT_KEY`, `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`, `RABBITMQ_USERNAME` and `RABBITMQ_PASSWORD` in `.env`.
   Follow the password requirements in the template; use a random JWT key of at least 32 UTF-8 bytes.
   Keep the supplied database, issuer, audience and port defaults unless needed. Use a non-guest RabbitMQ user and single-quote values containing `$` or `#`. `.env` is ignored by Git; leave `.env.example` as the shareable template.

3. Stop the standalone RabbitMQ stack if running, then build and start the full application:

   ```powershell
   docker compose -f docker-compose.rabbitmq.yml down
   docker compose build
   docker compose up -d --wait --wait-timeout 240
   docker compose ps -a
   ```

Open **http://localhost:8080** and sign in with `SEED_ADMIN_EMAIL` and `SEED_ADMIN_PASSWORD`. RabbitMQ management is **http://localhost:15672**; AMQP uses port `5672`. The API and SQL Server are internal to the Docker network.

Database migrations run automatically. The `migrate` container exiting with code `0` is expected. Stop the stack with `docker compose down`.

**Temporary verification (local only):** `artifacts/docker-verification/compose.env` and project `employee-management-docker-checks` use frontend port `20080` and RabbitMQ ports `25672`/`25673`. These optional settings are separate from the normal `.env` deployment above.

## Local development

1. Open `EmployeeManagement.sln` in Visual Studio, start SQL Server and restore packages:

   ```powershell
   dotnet restore EmployeeManagement.sln
   ```

2. Right-click **EmployeeManagement.Server > Manage User Secrets** and add the following, replacing placeholders and adjusting the SQL Server instance:

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

   The Administrator password needs at least six characters, including uppercase, lowercase, a digit and a symbol. JWT issuer/audience use the defaults in `appsettings.json`. Your Windows account needs database creation/update permissions.

3. Apply migrations in **Tools > NuGet Package Manager > Package Manager Console** before starting the API:

   ```powershell
   Update-Database -Project EmployeeManagement.Server -StartupProject EmployeeManagement.Server -Context ApplicationDbContext
   ```

4. Create/update root `.env` with `RABBITMQ_USERNAME` and `RABBITMQ_PASSWORD` matching User Secrets. Keep broker ports `5672`/`15672` and stop the full Docker deployment if it occupies them. Start RabbitMQ:

   ```powershell
   docker compose -f docker-compose.rabbitmq.yml up -d
   ```

5. Run `dotnet dev-certs https --trust` once. Set **EmployeeManagement.Server** as the startup project, select **https** and press **F5**. API: **https://localhost:7199**; Swagger: **https://localhost:7199/swagger**.

6. In another terminal, starting from the repository root, run React:

   ```powershell
   cd EmployeeManagement.Client
   npm.cmd ci
   npm.cmd run dev
   ```

Open **http://localhost:5173** and sign in with the Administrator from User Secrets. Keep the API running. Use `npm` instead of `npm.cmd` outside Windows.

## Unit tests

`tests/EmployeeManagement.UnitTests` uses xUnit, Moq and SQLite in memory to test critical task permissions and project rules. SQL Server and RabbitMQ are not required.

From the repository root:

```powershell
dotnet test EmployeeManagement.sln
```

`ProfileApiChecks` and `RabbitMqChecks` are separate console integration checks; `dotnet test` does not run them.
