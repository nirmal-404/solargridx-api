# SolarGridX API starter

Scaffold only: ASP.NET Core 10 controller-based REST API, MongoDB.Driver and JWT bearer dependencies, development OpenAPI, and configurable web CORS. No assignment endpoints, authentication flows, database access or business rules are implemented.

## Architecture

React web / native Kotlin Android -> HTTPS REST API hosted in IIS -> MongoDB.

- Controllers: HTTP request/response handling.
- Services: all authoritative business rules (FAT Service Pattern).
- Repositories: MongoDB persistence and atomic operations supporting service rules.
- Models and DTOs: persisted records and API contracts.
- Configuration, Middleware, Utilities: supporting infrastructure.

## Starter verification

Requires .NET SDK 10.0.300 or a later 10.0.3xx patch. Run `dotnet restore`, `dotnet build`, then `dotnet run --launch-profile https`. Local URLs: https://localhost:7019 and http://localhost:5205. The development document is `/openapi/v1.json`; no Swagger UI or domain routes are present. Trust the development HTTPS certificate through your normal .NET setup before using HTTPS locally.

MongoDB and JWT packages are installed but intentionally not wired up. Students must supply connection settings through user secrets/environment variables, implement token validation and role policies, then enable authentication middleware. Never commit connection credentials or signing secrets. CORS alone is not authentication.

## IIS deployment planning

Publish a Release build to a folder; use an IIS application pool with No Managed Code and the matching .NET 10 Hosting Bundle. The Web SDK generates web.config during publish. Configure HTTPS bindings, production environment, allowed web origins via Cors__AllowedOrigins__0, and server-only MongoDB credentials. Both clients must reach the API host; only the API host needs MongoDB access. Actual IIS provisioning and database setup are not included.

## Student handoff

Students must author feature code, every required .cs header, explanatory method-opening comments, and final reproducible setup instructions. Program.cs is starter infrastructure with top-level statements. Record AI-assisted scaffolding accurately in the disclosure; do not attribute generated scaffolding to independent student implementation.

Reference: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0
