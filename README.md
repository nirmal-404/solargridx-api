# SolarGridX API

Central ASP.NET Core 10 REST API for the Smart Solar Microgrid Trading System. Web and native Android clients use this API only; MongoDB is never accessed directly by a client. Services are intentionally "fat": all authorization, ownership checks, capacity checks, reservation transitions, time rules and QR checks happen on the server.

The only assignable staff roles are `Backoffice` and `GridOperator`. A Prosumer is an NIC-keyed customer profile, not a third role: Prosumer accounts carry no staff role claim, and the API checks profile/reservation ownership by authenticated account ID and NIC.

## Architecture

React web / native Kotlin Android -> HTTPS REST API hosted in IIS -> MongoDB.

- `Controllers`: HTTP, authorization and responses only.
- `Services`: authoritative workflows and business rules.
- `Domain`: MongoDB entities and strongly typed role/status enums.
- `Infrastructure`: MongoDB collections and startup indexes.
- `DTOs`: request and response contracts; password hashes and QR token hashes are never returned.
- `Middleware`: consistent safe API errors.

## Prerequisites and local setup

Requires .NET SDK 10.0.300+, MongoDB 8+ and a local HTTPS development certificate for HTTPS use. `appsettings.json` has a local MongoDB example but deliberately contains no JWT secret. Copy `.env.example` to `.env`, then set `Jwt__Secret` to a random value of at least 32 characters. The API loads `.env` before startup; the file is ignored by Git. Environment variables set by the operating system or deployment host take precedence over `.env`.

Run `dotnet restore`, `dotnet build`, then `dotnet run --launch-profile https`. Swagger UI is available in Development at `/swagger`; health is `/health`. On startup, the API creates the four required collections on use and their indexes: `users`, `solarStations`, `energyBookingSlots` and `energyReservations`.

## Endpoint overview

| Group | Main routes |
| --- | --- |
| Authentication | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Prosumer and users | `/api/prosumers/{nic}`, `/api/prosumers/pending`, `/api/prosumers/{nic}/reactivate`, `POST /api/users` |
| Stations and slots | `/api/stations`, `/api/stations/nearby`, `/api/slots`, `/api/stations/{stationId}/slots` |
| Reservations | `/api/reservations`, `/api/reservations/{id}/approve`, `/cancel`, `/reject`, `/transaction-token` |
| Transactions | `POST /api/transactions/verify`, `POST /api/transactions/{id}/complete` |
| Dashboard | `GET /api/dashboard/summary` |

Key rules: NIC and email are unique; a new reservation must start in the next seven days; update/cancel requires at least twelve hours; active means `Pending` or `Approved` for station-deactivation checks; capacity is deducted by an atomic conditional MongoDB update; a completed QR transaction cannot complete again.

## Security and operations

Passwords use PBKDF2 with a random salt. JWTs contain only subject, role and optional NIC claims. QR tokens are random opaque values and only their SHA-256 hashes are persisted. CORS origins are configured, HTTPS is enabled, and error responses avoid database and stack-trace details. Do not log passwords, JWTs, raw QR tokens or secrets.

## IIS publishing

Publish a Release build to a folder. Install the matching ASP.NET Core 10 Hosting Bundle on IIS, use an application pool with **No Managed Code**, configure HTTPS bindings, set `ASPNETCORE_ENVIRONMENT=Production`, and provide server-only MongoDB/JWT/CORS environment variables. The publish output includes the IIS `web.config`. Verify `/health`, then authenticated Swagger/API access from the intended web and mobile networks.

## Known limitations and handoff

The QR payload is returned by the owner-only transaction-token route and must be rendered by the mobile client. MongoDB atomic capacity updates protect normal booking conflicts; a replica-set-backed MongoDB transaction is the next enhancement for strict cross-document all-or-nothing reservation moves. Optional development seeding and automated test coverage remain to be completed before production use.

This API was generated with AI assistance at the user's explicit request. Record that assistance accurately according to course policy.

Reference: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0
