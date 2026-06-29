# TenantCore

A multi-tenant SaaS project manager where many companies share one deployment but can never see each other's data — built with ASP.NET Core (.NET 10) and a React + TypeScript frontend.

I built this because "multi-tenancy" gets thrown around a lot and I wanted to actually implement the hard part myself: making it *impossible* to leak one tenant's data into another, instead of relying on remembering to add `WHERE tenant_id = ...` to every query. The interesting code is all in how isolation is enforced at the data layer.

The app itself is a basic project/task manager — companies sign up, create projects and tasks, and invite members with different roles. That's deliberately ordinary. The point of the project is the tenancy and auth plumbing underneath it.

## Running it

You need Docker. Then:

```bash
git clone https://github.com/MohammadAlfalah/tenantcore
cd tenantcore
docker compose up --build
```

That brings up three containers — PostgreSQL, the API, and the React app behind nginx:

| Service  | URL                           |
| -------- | ----------------------------- |
| Frontend | http://localhost:3000         |
| API      | http://localhost:8080         |
| Swagger  | http://localhost:8080/swagger |

The API runs its EF Core migrations on startup, so the schema is created for you. There's no seed data — register a workspace at the frontend and you become that tenant's first Admin. If you want to watch isolation work, open an incognito window, register a *second* company, and confirm the two never overlap.

Compose ships with working defaults so `docker compose up` runs with no `.env`. The one thing you should change before putting this anywhere real is `JWT_SECRET` — copy `.env.example` to `.env` and set your own (it's a labeled `CHANGE_ME` placeholder by default).

## How tenant isolation actually works

This is the part I cared about. It's a shared-database, shared-schema design — every tenant-owned row has a `TenantId` column — and isolation is enforced in one place, the `AppDbContext`, so individual queries can't get it wrong.

- Every tenant-owned entity (`User`, `Project`, `ProjectTask`, `RefreshToken`) implements an `ITenantScoped` marker interface.
- After auth, `TenantMiddleware` pulls the `tenant_id` claim off the JWT into a scoped `ITenantContext`.
- The DbContext adds a global query filter `e => e.TenantId == CurrentTenantId` to every scoped entity, so EF Core appends the tenant check to *every* read automatically. With no tenant in scope, the filter matches zero rows — it fails closed.
- `SaveChanges` is overridden to stamp `TenantId` on new rows and throw if any insert/update has a `TenantId` that doesn't match the current tenant. So even a tampered request body can't write across tenants.

There are tests that specifically try to break this — reading and writing across two tenants — and assert it doesn't work (`TenantIsolationTests` for the DbContext level, `TenantIsolationApiTests` through the full HTTP pipeline).

## Auth

Registration creates a tenant and its first Admin in one step. Login issues a short-lived access token (15 min) plus a refresh token persisted server-side (7 days). Refresh tokens rotate — every `/api/auth/refresh` revokes the old one and issues a new one, so a leaked refresh token has a limited window.

The trickier bit was the frontend: the Axios client attaches the access token, and on a 401 it transparently refreshes and retries. Because tokens rotate, I had to make concurrent 401s share a single in-flight refresh instead of each firing their own — otherwise the second refresh fails against an already-rotated token. That logic lives in `frontend/src/api/client.ts`.

## Roles

Three roles, scoped per tenant — Viewer (read), Member (read + tasks), Admin (everything, including managing projects and members). They're enforced server-side with authorization policies on the controllers; the UI hides buttons a role can't use, but the API is the real gate, so a forged request from a Viewer still gets a 403. `RoleBasedAccessTests` covers this. There are also a couple of guardrails I added because they bit me in testing — an Admin can't delete their own account, and a tenant can't be left with zero Admins.

## Stack

- **Backend:** ASP.NET Core (.NET 10), EF Core + Npgsql on PostgreSQL 17, JWT bearer auth, BCrypt for password hashing. Organized by feature (Auth / Projects / Tasks / Members), each with thin controllers over injectable services.
- **Frontend:** React 19, TypeScript, Vite, Tailwind, React Router, Axios.
- **Tests:** xUnit + FluentAssertions. Integration tests run against the EF Core in-memory provider through `WebApplicationFactory`, so CI needs no database container.
- **CI:** GitHub Actions builds and tests the backend, lints and builds the frontend, and builds both Docker images on every push/PR to `main`.

## Running pieces individually (without Docker)

Start just Postgres with `docker compose up -d db`, then:

```bash
# backend
cd backend
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=tenantcore;Username=tenantcore;Password=tenantcore"
export Jwt__Secret="a-dev-secret-at-least-32-characters-long"
dotnet run --project src/TenantCore.Api      # http://localhost:5048, /swagger

# frontend (separate terminal)
cd frontend
npm install
npm run dev                                   # http://localhost:5173, proxies /api to the backend
```

Run the test suite with `dotnet test` from the `backend` folder.

## What I'd add next

It's a portfolio project, so a few things are intentionally left out: there's no real email flow for member invites (an Admin just creates the account directly), no audit log, and tenant resolution is purely claim-based rather than subdomain/header. If I took this further, server-side email invites and a per-tenant audit trail would be the first additions.
