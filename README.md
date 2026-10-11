# SchoolManagementSystem

A .NET 8 ASP.NET Core MVC project for school administration and academic record management. This repository includes the MVC application, EF Core data layer and migrations, selected unit tests, and manual/Postman testing materials.

## Project Overview

The application uses ASP.NET Core Identity for account workflows and role-based access, Entity Framework Core with SQL Server for persistence, and MVC pages for school-management tasks. The repository also contains integrations/configuration points for email, Google reCAPTCHA, Azure Storage, and Syncfusion. Those external integrations have not been verified against live services.

## Key Features

- Account registration, email confirmation, login with OTP, password recovery, and role management.
- MVC workflows for students, teachers, employees, courses, classes, subjects, attendance, grades, and payments.
- Role-specific dashboards, alerts, and activity history.
- A students-by-class HTTP API endpoint.
- Unit tests for selected OTP, authorization/antiforgery, mail-test-policy, role-policy, and view-model/helper behavior.

## Tech Stack

- .NET 8 and ASP.NET Core MVC
- ASP.NET Core Identity EF Core 8.0.8
- Entity Framework Core SQL Server and Tools 8.0.31
- MailKit 4.17.0
- Azure Storage packages and Azure.Identity 1.21.0
- Syncfusion EJ2 ASP.NET Core 27.1.51
- xUnit

## Architecture / Project Structure

- `SchoolManagementSystem/Controllers/` — MVC controllers and the API controller.
- `SchoolManagementSystem/Data/` — EF Core context, entities, and seed-related code.
- `SchoolManagementSystem/Migrations/` — EF Core migration classes and model snapshot.
- `SchoolManagementSystem/Helpers/` and `Repositories/` — application helpers and data-access abstractions/implementations.
- `SchoolManagementSystem/Models/` — entities and MVC view models.
- `SchoolManagementSystem/Views/` and `wwwroot/` — Razor views and static assets.
- `SchoolManagementSystem.Tests/` — unit tests.
- `docs/testing/` — manual test cases, a Postman collection, and a sanitized example environment.
- `SchoolManagementSystem/appsettings.example.json` — configuration key reference with empty values or placeholders.

## Prerequisites

- .NET 8 SDK
- SQL Server for database-backed workflows
- Optional sandbox/local services for SMTP, Azure Storage, and reCAPTCHA
- A Syncfusion license key if using features that require Syncfusion licensing

## Local Setup

1. Install the .NET 8 SDK and provide a SQL Server instance dedicated to local development or testing.
2. Review `SchoolManagementSystem/appsettings.example.json` for configuration key names. Keep real values in .NET User Secrets or environment variables; do not commit populated settings.
3. Set local values privately from the repository root. Replace each placeholder locally; do not paste real values into this README or committed files.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<LOCAL_SQL_CONNECTION>" --project .\SchoolManagementSystem\SchoolManagementSystem.csproj
dotnet user-secrets set "Tokens:Issuer" "<LOCAL_ISSUER>" --project .\SchoolManagementSystem\SchoolManagementSystem.csproj
dotnet user-secrets set "Tokens:Audience" "<LOCAL_AUDIENCE>" --project .\SchoolManagementSystem\SchoolManagementSystem.csproj
dotnet user-secrets set "Tokens:Key" "<LOCAL_SIGNING_KEY>" --project .\SchoolManagementSystem\SchoolManagementSystem.csproj
```

Optional integrations use the `Mail`, `GoogleReCAPTCHA`, `Blob`, and `Syncfusion` configuration groups shown in the example settings. Supply sandbox values only when testing those integrations.

**Database safety:** normal application startup includes database creation and seed logic. Do not use a production, shared, or personal database for local startup. The application does not automatically apply EF Core migrations. Use a disposable test database and review initialization behavior before running the app.

## Build & Test

Run from the repository root:

```powershell
dotnet restore .\SchoolManagementSystem.sln
dotnet build .\SchoolManagementSystem.sln --no-restore
dotnet test .\SchoolManagementSystem.Tests\SchoolManagementSystem.Tests.csproj --no-restore
```

Last verified in this publication copy before this README-only update: build succeeded with 0 errors and 99 warnings; **30 tests passed, 0 failed, 0 skipped**.

## API Testing Example

`StudentsApiController` exposes `GET /api/StudentsApi/BySchoolClass/{schoolClassId}`. It returns a JSON list for a class with matching students, or `404 Not Found` when none are found. For example, after configuring and starting the app against a disposable test database:

```powershell
$baseUrl = "https://localhost:<PORT>"
$schoolClassId = 1
Invoke-RestMethod -Method Get -Uri "$baseUrl/api/StudentsApi/BySchoolClass/$schoolClassId"
```

Use the actual local URL/port configured for your environment. This endpoint requires the application and database to be running; do not use production data for this example.

## Test Coverage & Limitations

The current unit tests cover selected policies and logic, including OTP expiry/single use/attempt limits and binding, role assignment/deletion policies, controller authorization/antiforgery metadata, restrictions in the test-mail helper, and view-model/helper behavior. They do not provide full MVC integration coverage or verify repository behavior against a database.

- Build warnings remain, primarily nullable-reference warnings.
- Database initialization, seed behavior, and migrations have not been tested against a disposable database.
- Migration metadata includes EF Core 9 preview references while the project packages use EF Core 8. Review compatibility before applying migrations.
- SMTP delivery, Azure Storage, reCAPTCHA, Syncfusion licensing, and other external-service behavior have not been verified with live or sandbox services.
- No application screenshots are included. Add genuine screenshots after capturing them from a safe, non-production environment; do not use real student or account data.

## Security Notes

- `SchoolManagementSystem/appsettings.json` is local-only and ignored. Never commit populated secrets, tokens, passwords, license keys, private keys, or connection strings.
- Use .NET User Secrets for local development and the hosting platform's secret store for deployment. Environment variables can use the standard double-underscore format for hierarchical .NET configuration keys.
- The committed example configuration and Postman environment are templates only. Keep local Postman environments and test credentials out of version control.
- This repository copy has no application-level open-source license. Third-party assets retain their own license terms.
