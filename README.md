# TenantCore

A full-stack, **multi-tenant B2B SaaS** project-management platform. Multiple companies (tenants)
share one deployment, but each tenant's data, users, and roles are **completely isolated** — a user
of one tenant can never see or touch another tenant's data.

- **Backend:** ASP.NET Core (.NET 10), PostgreSQL, EF Core, JWT auth, role-based access control.
- **Frontend:** React + TypeScript, Tailwind CSS, Axios with automatic token refresh.
- **Runs with a single command:** `docker compose up`.

---

## Table of contents

- [Quick start](#quick-start)
- [Architecture](#architecture)
- [How tenant isolation works](#how-tenant-isolation-works)
- [Authentication & token refresh](#authentication--token-refresh)
- [Roles & permissions](#roles--permissions)
- [API reference](#api-reference)
- [Project structure](#project-structure)
- [Running locally without Docker](#running-locally-without-docker)
- [Testing](#testing)
- [Configuration](#configuration)
- [CI](#ci)

---

## Quick start

**Prerequisites:** [Docker](https://docs.docker.com/get-docker/) + Docker Compose.

```bash
git clone <this-repo>
cd TenantCore
docker compose up --build
```

That's it. Compose starts three containers:

| Service    | URL                              | Description                          |
| ---------- | -------------------------------- | ------------------------------------ |
| `frontend` | http://localhost:3000            | React app (served by nginx)          |
| `api`      | http://localhost:8080            | REST API                             |
| `api` docs | http://localhost:8080/swagger    | Swagger / OpenAPI UI                 |
| `db`       | localhost:5432                   | PostgreSQL 17                        |

The API **applies its EF Core migrations automatically on startup**, so the database schema is
created for you. There is **no seed/placeholder data** — everything is created through the app:

1. Open http://localhost:3000 and click **Create a workspace**.
2. Fill in a company name, your name, email, and password → you become that tenant's first **Admin**.
3. Create projects, add tasks, and invite members from the **Members** page.
4. To see isolation in action, open a second browser/incognito window, register a _different_
   company, and confirm the two tenants never see each other's data.

> **Security note:** the default `JWT_SECRET` is a development placeholder. Set a strong secret via a
> `.env` file (see [`.env.example`](.env.example)) before deploying anywhere real.

---

## Architecture

```
┌─────────────────┐        ┌──────────────────────────────────────────────┐        ┌────────────┐
│   React + TS    │  HTTPS │           ASP.NET Core API (.NET 10)          │  TCP   │ PostgreSQL │
│   (nginx :80)   │ ─────► │                                              │ ─────► │    :5432   │
│                 │ /api/* │  Auth ─ JWT issue/validate/refresh           │        │            │
│  Axios client   │        │  TenantMiddleware ─ reads tenant_id claim    │        │  tenants   │
│  • attaches JWT │        │  AppDbContext ─ GLOBAL QUERY FILTERS by tenant│        │  users     │
│  • auto-refresh │        │  RBAC policies ─ Admin / Member / Viewer     │        │  projects  │
└─────────────────┘        └──────────────────────────────────────────────┘        │  tasks     │
                                                                                    │ refresh_tk │
                                                                                    └────────────┘
```

In Docker, nginx serves the built React app **and** reverse-proxies `/api/*` to the API container.
Because the browser only ever talks to nginx (same origin), there is no CORS in the container setup.

### Backend layering (separation of concerns)

```
Domain          → entities, enums, the ITenantScoped marker interface (no dependencies)
Infrastructure  → EF Core DbContext + query filters, tenant context, JWT, password hashing
Features         → one folder per use case (Auth, Projects, Tasks, Members):
                   DTOs + Service (business logic) + Controller (thin HTTP layer)
Common          → cross-cutting: typed exceptions + global ProblemDetails handler
```

Controllers are thin; all business logic lives in injectable **services** (`AuthService`,
`ProjectService`, `TaskService`, `MemberService`) which are unit-testable in isolation.

---

## How tenant isolation works

TenantCore uses the **shared-database, shared-schema** model: every tenant-owned row carries a
`TenantId` column. Isolation is enforced **automatically by the data layer**, not by remembering to
add a `WHERE` clause:

1. **A marker interface.** Every tenant-owned entity implements
   [`ITenantScoped`](backend/src/TenantCore.Api/Domain/Common/ITenantScoped.cs) (`User`, `Project`,
   `ProjectTask`, `RefreshToken`).

2. **A per-request tenant context.** After authentication,
   [`TenantMiddleware`](backend/src/TenantCore.Api/Infrastructure/Tenancy/TenantMiddleware.cs) reads
   the `tenant_id` claim from the JWT and stores it in a scoped
   [`ITenantContext`](backend/src/TenantCore.Api/Infrastructure/Tenancy/ITenantContext.cs).

3. **Global query filters.**
   [`AppDbContext`](backend/src/TenantCore.Api/Infrastructure/Data/AppDbContext.cs) adds
   `HasQueryFilter(e => e.TenantId == CurrentTenantId)` to every scoped entity. EF Core appends this
   to **every** query automatically — so `db.Projects.ToList()` only ever returns the current
   tenant's projects. With no tenant in scope, the filter matches **zero** rows (fail closed).

4. **Write-side enforcement.** `SaveChangesAsync` stamps `TenantId` on new rows and **rejects** any
   insert/update whose `TenantId` doesn't match the current tenant — defense in depth against a
   tampered request body.

The result: a developer literally cannot write a query that leaks another tenant's data. This is
proven by [`TenantIsolationTests`](backend/tests/TenantCore.Tests/Unit/TenantIsolationTests.cs) and
[`TenantIsolationApiTests`](backend/tests/TenantCore.Tests/Integration/TenantIsolationApiTests.cs).

---

## Authentication & token refresh

- **Registration** (`POST /api/auth/register`) creates a new **tenant** + its first **Admin** user
  in one step.
- **Login** issues a short-lived **access token** (JWT, 15 min) and a long-lived **refresh token**
  (persisted server-side, 7 days).
- The JWT embeds `sub` (user id), `tenant_id`, `role`, `email`, and `name`.
- **Refresh tokens rotate**: each call to `POST /api/auth/refresh` revokes the old token and issues a
  new one, so a stolen refresh token has a limited blast radius.

On the frontend, the Axios client
([`api/client.ts`](frontend/src/api/client.ts)) attaches the access token to every request and, on a
`401`, transparently refreshes and retries. Concurrent 401s share a **single in-flight refresh**
(important because tokens rotate) — see the comments in that file.

Email is **globally unique**, so a person belongs to exactly one tenant and login is a simple
email + password (no "which company?" prompt). Cross-tenant access is still impossible because the
issued token is scoped to the user's tenant and every query is filtered by it.

---

## Roles & permissions

Each user has one role **within their tenant**. Roles never cross tenant boundaries.

| Capability                     | Viewer | Member | Admin |
| ------------------------------ | :----: | :----: | :---: |
| View projects & tasks          |   ✅   |   ✅   |  ✅   |
| Create / edit / delete tasks   |   —    |   ✅   |  ✅   |
| Create / edit / delete projects|   —    |   —    |  ✅   |
| Manage members (invite/role)   |   —    |   —    |  ✅   |

Enforced on the **server** via authorization policies
([`AuthorizationPolicies`](backend/src/TenantCore.Api/Infrastructure/Auth/AuthorizationPolicies.cs))
applied to controller actions. The **UI** mirrors these rules (hiding buttons a role can't use via
`RoleGate`), but the API is the source of truth — a forged request from a Viewer still gets a `403`.
Proven by [`RoleBasedAccessTests`](backend/tests/TenantCore.Tests/Integration/RoleBasedAccessTests.cs).

---

## API reference

Base URL: `http://localhost:8080`. Full interactive docs at `/swagger`.

### Auth

| Method | Route                | Auth        | Description                                  |
| ------ | -------------------- | ----------- | -------------------------------------------- |
| POST   | `/api/auth/register` | Anonymous   | Create a tenant + first Admin; returns tokens|
| POST   | `/api/auth/login`    | Anonymous   | Log in; returns tokens                       |
| POST   | `/api/auth/refresh`  | Anonymous   | Rotate refresh token; returns new tokens     |
| POST   | `/api/auth/logout`   | Bearer      | Revoke a refresh token                       |
| GET    | `/api/auth/me`       | Bearer      | Current user + tenant                        |

### Projects

| Method | Route                  | Role        | Description                  |
| ------ | ---------------------- | ----------- | ---------------------------- |
| GET    | `/api/projects`        | Any         | List tenant's projects       |
| GET    | `/api/projects/{id}`   | Any         | Project detail with tasks    |
| POST   | `/api/projects`        | Admin       | Create a project             |
| PUT    | `/api/projects/{id}`   | Admin       | Update a project             |
| DELETE | `/api/projects/{id}`   | Admin       | Delete a project (+ tasks)   |

### Tasks

| Method | Route                              | Role          | Description            |
| ------ | ---------------------------------- | ------------- | ---------------------- |
| GET    | `/api/projects/{projectId}/tasks`  | Any           | List a project's tasks |
| POST   | `/api/projects/{projectId}/tasks`  | Admin/Member  | Create a task          |
| GET    | `/api/tasks/{id}`                  | Any           | Get a task             |
| PUT    | `/api/tasks/{id}`                  | Admin/Member  | Update a task          |
| PATCH  | `/api/tasks/{id}/status`           | Admin/Member  | Change task status     |
| DELETE | `/api/tasks/{id}`                  | Admin/Member  | Delete a task          |

### Members

| Method | Route                | Role  | Description              |
| ------ | -------------------- | ----- | ----------------------- |
| GET    | `/api/members`       | Any   | List tenant's members   |
| POST   | `/api/members`       | Admin | Invite/create a member  |
| PUT    | `/api/members/{id}`  | Admin | Update name/role        |
| DELETE | `/api/members/{id}`  | Admin | Remove a member         |

Guardrails: an Admin cannot remove their own account, and a tenant can never lose its last Admin.

---

## Project structure

```
TenantCore/
├── docker-compose.yml          # one command brings up db + api + frontend
├── .env.example                # configuration template
├── .github/workflows/ci.yml    # backend tests, frontend build, docker build
│
├── backend/
│   ├── Dockerfile
│   ├── TenantCore.slnx
│   ├── src/TenantCore.Api/
│   │   ├── Domain/             # entities, enums, ITenantScoped
│   │   ├── Infrastructure/
│   │   │   ├── Data/           # AppDbContext (global query filters), design-time factory
│   │   │   ├── Tenancy/        # ITenantContext, TenantMiddleware
│   │   │   └── Auth/           # JWT service, password hasher, RBAC policies
│   │   ├── Features/           # Auth / Projects / Tasks / Members (DTO + Service + Controller)
│   │   ├── Common/             # typed exceptions + global exception handler
│   │   ├── Migrations/         # EF Core migrations (real, generated)
│   │   └── Program.cs          # composition root & HTTP pipeline
│   └── tests/TenantCore.Tests/
│       ├── Unit/               # tenant isolation, member guardrails
│       └── Integration/        # full HTTP pipeline via WebApplicationFactory
│
└── frontend/
    ├── Dockerfile
    ├── nginx.conf              # serves the SPA + proxies /api to the api container
    └── src/
        ├── api/                # axios client (auto-refresh) + per-resource modules
        ├── auth/               # AuthContext + token storage
        ├── components/         # Layout, ProtectedRoute, RoleGate, UI primitives
        ├── pages/              # Login, Register, Dashboard, ProjectDetail, Members
        ├── lib/                # role helpers
        └── types/              # TypeScript types mirroring the API DTOs
```

---

## Running locally without Docker

You can run each piece directly for development.

### 1. Database

Start just Postgres (or point at your own):

```bash
docker compose up -d db
```

### 2. Backend

```bash
cd backend
# Connection string + secret can come from appsettings.json or env vars:
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=tenantcore;Username=tenantcore;Password=tenantcore"
export Jwt__Secret="a-strong-development-secret-at-least-32-characters"
dotnet run --project src/TenantCore.Api
# API → http://localhost:5048, Swagger → http://localhost:5048/swagger
```

Migrations apply automatically on startup. To manage them by hand:

```bash
dotnet ef migrations add <Name> --project src/TenantCore.Api
dotnet ef database update   --project src/TenantCore.Api
```

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
# App → http://localhost:5173 ; Vite proxies /api → http://localhost:5048
```

---

## Testing

```bash
cd backend
dotnet test
```

The suite (xUnit + FluentAssertions) covers:

- **Unit** — tenant isolation at the DbContext level (query filters + write-side stamping) and member
  management guardrails (last-admin / self-deletion protection).
- **Integration** — the full HTTP pipeline (auth, refresh rotation, tenant isolation across two
  tenants, and the three-tier RBAC) through `WebApplicationFactory`, using the EF Core in-memory
  provider so **no database container is required** in CI.

---

## Configuration

All configuration is environment-variable friendly (12-factor). Key settings:

| Variable                    | Default                              | Purpose                              |
| --------------------------- | ------------------------------------ | ------------------------------------ |
| `ConnectionStrings__Default`| (see compose)                        | PostgreSQL connection string         |
| `Jwt__Secret`               | dev placeholder                      | HMAC signing key (**≥ 32 chars**)    |
| `Jwt__AccessTokenMinutes`   | `15`                                 | Access token lifetime                |
| `Jwt__RefreshTokenDays`     | `7`                                  | Refresh token lifetime               |
| `Cors__AllowedOrigins__0`   | `http://localhost:5173`              | Allowed browser origin(s)            |

For Docker, copy [`.env.example`](.env.example) to `.env` and edit. Compose also ships safe defaults,
so `docker compose up` works with no `.env` at all (except you should still set a real `JWT_SECRET`).

---

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push/PR:

1. **Backend** — restore, build (Release), run all xUnit tests.
2. **Frontend** — `npm ci`, ESLint, production build.
3. **Docker** — build both images to verify the Dockerfiles.
