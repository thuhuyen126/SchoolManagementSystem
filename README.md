# SchoolManagementSystem

A .NET 8 ASP.NET Core MVC portfolio project for school administration and academic record management.

## Overview

The application combines ASP.NET Core Identity, role-based MVC workflows, Entity Framework Core with SQL Server, and integrations for email, Google reCAPTCHA, Azure Blob Storage, and Syncfusion controls.

## Features

- Account registration, email confirmation, login with OTP, password recovery, and role management.
- Student, teacher, employee, course, class, subject, attendance, grade, and payment workflows.
- Alerts, activity history, and role-specific dashboards.
- An HTTP endpoint for retrieving students by class.
- Unit tests for selected security policies, OTP behavior, and view-model/helper logic.

## Technology

- .NET 8 and ASP.NET Core MVC
- Entity Framework Core SQL Server 8.0.31
- ASP.NET Core Identity EF Core 8.0.8
- MailKit 4.17.0
- Azure Storage SDKs
- Syncfusion EJ2 ASP.NET Core 27.1.51
- xUnit

## Repository layout

- `SchoolManagementSystem/` — MVC application, data layer, migrations, and static assets.
- `SchoolManagementSystem.Tests/` — automated unit tests.
- `docs/testing/` — manual test cases, a Postman collection, and a sanitized example environment.
- `SchoolManagementSystem/appsettings.example.json` — configuration key template with placeholders only.

## Prerequisites

- .NET 8 SDK
- SQL Server for database-backed application workflows
- A local SMTP sink such as Mailpit for email testing
- Sandbox settings for optional reCAPTCHA, Azure Blob Storage, and Syncfusion features

## Local configuration

Use `SchoolManagementSystem/appsettings.example.json` as a key reference. Keep real values in .NET User Secrets or environment variables; do not commit populated settings.

From the repository root, configure local database and token settings privately:

    dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<LOCAL_SQL_CONNECTION>" --project SchoolManagementSystem/SchoolManagementSystem.csproj
    dotnet user-secrets set "Tokens:Issuer" "<LOCAL_ISSUER>" --project SchoolManagementSystem/SchoolManagementSystem.csproj
    dotnet user-secrets set "Tokens:Audience" "<LOCAL_AUDIENCE>" --project SchoolManagementSystem/SchoolManagementSystem.csproj
    dotnet user-secrets set "Tokens:Key" "<LOCAL_SIGNING_KEY>" --project SchoolManagementSystem/SchoolManagementSystem.csproj

Optional integrations also use Mail, GoogleReCAPTCHA, Blob, and Syncfusion configuration. See the example settings for key names. User Secrets are for local development; use the deployment platform's secret store for deployed environments.

## Build and test

    dotnet restore SchoolManagementSystem.sln
    dotnet build SchoolManagementSystem.sln --no-restore
    dotnet test SchoolManagementSystem.Tests/SchoolManagementSystem.Tests.csproj --no-restore

The automated tests are unit tests. A successful build and unit-test run does not verify database migrations, SMTP delivery, Azure services, reCAPTCHA, or every application workflow.

## Database safety

Do not point the application at a production, shared, or personal database during initial setup. Normal startup includes database creation and seed logic, which can create schema and insert initial data. The application does not automatically apply EF Core migrations. Use a dedicated disposable database and review initialization behavior before running the application.

## Current limitations

- Database and runtime integration need separate verification in an isolated environment.
- Nullable compiler warnings remain.
- External integrations require sandbox configuration.
- This portfolio copy does not include an application-level open-source license; third-party assets retain their own licenses.
