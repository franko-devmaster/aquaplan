# AquaPlan — Project Instructions

## Overview

AquaPlan is a web application for the digital management of water quality analysis orders and prescriptions for the Canton of Fribourg, Switzerland. It covers potable water and bathing water analyses. The platform integrates with the Limsophy LIMS (by AAC Infotray) for laboratory data exchange.

### Core Functional Domains

- **Sampling round planning** — schedule and organize sampling tours across communes
- **Sampling execution** — record field sampling data, send to LIMS
- **Analysis results** — consult results returned from Limsophy LIMS
- **Unplanned sampling** — handle ad-hoc sampling outside scheduled rounds
- **LIMS integration** — bidirectional data exchange with Limsophy (orders → LIMS, results ← LIMS)
- **Analysis catalog** — manage the catalog of available analysis types and parameters
- **Reference data** — manage communes, sampling locations, and related master data

## Tech Stack

- **Backend**: .NET 10, C# latest, ASP.NET Core, Entity Framework Core, PostgreSQL
- **Frontend**: Angular 19, TypeScript 5.6, SCSS
- **UI Libraries**: Angular Material, Bootstrap 5
- **Forms**: ngx-formly (JSON schema-driven forms)
- **i18n**: @ngx-translate (Angular), resource files (.resx) (.NET)
- **Logging**: Serilog (.NET), ngx-logger (Angular)
- **API Docs/Codegen**: NSwag (auto-generates TypeScript clients on debug build), Swashbuckle
- **Auth**: ASP.NET Identity, JWT, OIDC
- **Testing**: xUnit, FluentAssertions, Moq, MockQueryable.Moq, coverlet
- **Telemetry**: OpenTelemetry
- **Versioning**: GitVersion
- **Database**: PostgreSQL (via Npgsql + EF Core)
- **Containerization**: Docker, Docker Compose (dev), K8S (prod target on Exoscale)

## Solution Structure

Solution file: `AquaPlan.sln`

### Core Applications (runnable)

| Project | Description |
|---|---|
| `AquaPlan.Api` | Main backend Web API (ASP.NET Core) |
| `AquaPlan.Web` | Angular SPA frontend |
| `AquaPlan.Worker` | Background job runner (LIMS sync, notifications) |

### Class Libraries

| Project | Description |
|---|---|
| `AquaPlan.Domain` | Domain entities, enums, value objects |
| `AquaPlan.Application` | Business logic, services, DTOs, interfaces |
| `AquaPlan.Infrastructure` | EF Core DbContext, repositories, external integrations (Limsophy) |
| `AquaPlan.Shared` | Cross-cutting concerns: extensions, helpers, constants |

### Angular Application

| App | Path | UI Framework |
|---|---|---|
| AquaPlan Web | `AquaPlan.Web/ClientApp/` | Angular Material + Bootstrap |

### Tests

All test projects live in `tests/`. Shared test global usings in `tests/AquaPlan.Tests.Shared/Usings.cs` (FluentAssertions, xUnit, Moq, MockQueryable.Moq).

| Test Project | Covers |
|---|---|
| `tests/AquaPlan.Api.Tests` | API controllers, middleware |
| `tests/AquaPlan.Application.Tests` | Services, business logic |
| `tests/AquaPlan.Infrastructure.Tests` | Repositories, EF queries |

## .NET Coding Conventions

### General Style

- **C# latest features**: use records for DTOs, primary constructors, pattern matching, collection expressions
- **No expression-bodied methods/constructors** — block bodies only (expression-bodied properties/accessors are fine)
- **`nameof`** for string literals referencing identifiers
- **Nullable enabled** globally
- **Indent**: 4 spaces, CRLF line endings

### Architecture Patterns

- **Controllers**: `[Route("api/[controller]")]` + `[ApiController]` + `ControllerBase`
- **Primary constructors** on controllers (preferred for new code)
- **Services**: interface `IXxxService` + implementation `XxxService` (often `internal`)
- **DI registration**: extension methods on builder (`.WithXxx()` pattern)
- **API versioning**: `[ApiVersion("1.0")]`
- **Audit**: `[Audited]` attribute on auditable endpoints
- **Auth**: JWT with custom `JwtTenantAuthenticationHandler`, OIDC
- **NSwag**: auto-generates TypeScript API clients from .NET controllers on debug build

### Naming

- PascalCase for types, methods, properties
- `_camelCase` for private fields
- `I` prefix for interfaces
- Controllers: `XxxController` in `Controllers/Api/`
- Services: `IXxxService` / `XxxService` in `Services/` with `Interfaces/` subfolder
- DTOs: `XxxDto`, `XxxAddDto`, `XxxUpdateDto`, `XxxFilteringInputDto`, `XxxListDto`
- Extensions: `XxxExtensions` in `Extensions/`
- Test classes: `XxxTest` (e.g. `SamplingServiceTest`)

## .NET Testing Conventions

- **MANDATORY**: Always write unit tests when creating or modifying .NET code (controllers, services, etc.). Tests are not optional — they are part of the implementation. Do not consider a task complete without tests.
- **Framework**: xUnit + FluentAssertions + Moq
- **Naming**: `<MethodName>_Should<Expected>` or `<MethodName>_When<Condition>_Should<Expected>`
- **Structure**: abstract base test classes (e.g. `AbstractServiceProvidedTest`) with service provider setup
- **Mocking**: Moq + MockQueryable.Moq, fake implementations (e.g. `FakeUserManager`), shared mocks in `tests/AquaPlan.Tests.Shared/`
- **System under test**: name the field `_sut`
- **Mock fields**: declare at class level, configure in constructor, override per-test only when needed
- **`[Fact]`** for single cases, **`[Theory]`** + `[InlineData]` for parameterized — one behavior per test, concrete values (no random data)
- **FluentAssertions only** — never `Assert.*`
- **Exception assertions**: use `.Awaiting(x => x.Method()).Should().ThrowAsync<T>()`
- **Attribute tests**: always test HTTP method attributes (`[HttpGet]`, `[HttpPost]`, etc.), route templates, and auth attributes on controllers
- **No Angular unit tests** — Angular code is not unit tested in this project

## Angular Conventions

### Current Codebase Patterns

- **Standalone components** (Angular 19 default — no NgModules in the codebase)
- **Constructor injection** (`constructor(private readonly service: XxxService)`) in existing code
- **NSwag-generated API services** (`api-services.service.generated.ts`) — do NOT edit these files
- **App structure** (`Web/ClientApp/src/app/`): `pages/`, `components/`, `services/`, `models/`, `datastore/`, `guards/`, `handlers/`, `pipes/`, `directives/`, `utils/`, `types/`, `constants/`
- **Datastore pattern**: signal-based state management classes in `datastore/` (e.g., `SamplingDatastore`) using `signal()` + `inject()` + API service calls
- **Guards**: `AuthorizeGuard`, `FeatureGuard` for route protection
- **@ngx-translate** for internationalization
- **SCSS** for component styles
- **Mixed control flow**: existing templates use `*ngIf`/`*ngFor`; newer code uses `@if`/`@for`. When editing existing templates, match the surrounding style

### Guidelines for New Code

- Use Angular signals (`signal()`, `computed()`, `input()`, `output()`) for new components
- Use new control flow (`@if`, `@for`, `@switch`) in new templates
- Use `inject()` function in new components (instead of constructor injection)
- Use `ChangeDetectionStrategy.OnPush` on all new components
- Never use `any` type — use `unknown` with type guards or define proper types
- Do NOT write Angular unit tests

## Security

This is a cantonal government application handling environmental and public health data. Security is everyone's responsibility — every agent must follow these rules:

### Authentication & Authorization
- All new endpoints MUST have `[Authorize]` — deny by default, use `[AllowAnonymous]` only when explicitly required
- Use role-based authorization (`[Authorize(Roles = ...)]`) to restrict access appropriately
- Never bypass authentication checks or weaken existing auth requirements

### Input Validation
- Validate all user input at controller level using typed DTOs with data annotations
- Never use raw strings for complex input — use `[FromBody]`/`[FromQuery]` with strongly-typed DTOs
- Never build SQL queries, URLs, or file paths from string concatenation with user input
- Use parameterized queries (EF Core handles this) — never use raw SQL with interpolated user values

### Tenant Isolation
- Always filter queries by tenant context — never allow cross-tenant data access
- Verify tenant ownership when accessing or modifying resources

### Sensitive Data
- Never log passwords, tokens, PII, or secrets
- Never return sensitive fields (passwords, hashes, internal IDs) in API responses
- Use `[JsonIgnore]` on sensitive DTO properties
- Never commit secrets, connection strings, or API keys to source code

### Frontend Security
- Sanitize dynamic content to prevent XSS — use Angular's built-in sanitization, never bypass it with `bypassSecurityTrust*` unless absolutely necessary
- Use `HttpOnly` and `Secure` flags on cookies (already configured in Startup)
- Use the existing `requestInterceptor` for credential handling — never store tokens in localStorage

### General
- Follow the existing audit trail pattern — add `[Audited]` on endpoints that modify sensitive data
- Respect the existing `ApiExceptionFilterAttribute` error handling — never expose stack traces or internal details in API responses
- Use `[FeatureGate]` for features that should be conditionally enabled

## Principles

1. **KISS** — simplest solution that works
2. **YAGNI** — only implement what is explicitly required now
3. **DRY** — extract duplication only when proven (rule of three)
4. **SOLID** — apply judiciously, not dogmatically
5. **Minimal changes** — touch only what is strictly needed for the task

## Agents

This project uses specialized Claude Code agents for different roles. Agent definitions are in `/.claude/agents/`.

| Agent | Role | File |
|---|---|---|
| Team Lead | Architecture decisions, code review coordination, sprint planning | `/.claude/agents/team-lead.md` |
| PO (Product Owner) | Requirements, user stories, acceptance criteria, prioritization | `/.claude/agents/po.md` |
| Code Reviewer | Code quality, conventions enforcement, PR reviews | `/.claude/agents/code-reviewer.md` |
| DEV Senior | Implementation, debugging, technical solutions | `/.claude/agents/dev-senior.md` |
| QA Lead | Test strategy, test execution, bug tracking, non-regression | `/.claude/agents/qa-lead.md` |
| UX Lead | User journeys, UI consistency, design system, accessibility | `/.claude/agents/ux-lead.md` |
| Xray Test Manager | Xray Cloud operations, test suites, executions, Gherkin | `/.claude/agents/xray-tester.md` |
| Jira Updater | Jira status transitions, story/epic propagation, Git workflow | `/.claude/agents/jira-updater.md` |

## Skills

Skills are reusable workflows in `/.claude/skills/`.

| Skill | Description | File |
|---|---|---|
| Execute Gherkin Tests | Automated execution of Xray Gherkin scenarios in the running app | `/.claude/skills/execute-gherkin-tests.md` |

## Version Lifecycle — Mandatory Process

**Every version MUST follow this lifecycle. No step can be skipped.**

### Before implementation (Definition of Ready)
1. **Jira tickets → "En cours"**: All stories of the version must be transitioned from Backlog → En cours BEFORE any code is written. The parent epic must also be in En cours.

### After implementation (Definition of Done — code)
2. **Commit with AQ-xxx keys**: Every commit message must reference Jira ticket keys
3. **Push to Bitbucket immediately**: `git push origin Main` — code not on Bitbucket is invisible
4. **Jira tickets → "Terminé"**: Stories transitioned after code is pushed
5. **Verify Jira Development tab**: Commits must appear linked to stories

### After tests (Definition of Done — QA)
6. **Test Set + Test Plan + Test Execution** created in Xray for the version
7. **Non-regression plan AQ-194 updated**: new tests added
8. **Non-regression execution created and run**: all cumulative tests pass
9. **Results imported into Xray**

**Rule: A version with unpushed code, un-updated Jira tickets, or missing test executions is NOT done.**

## Quality Assurance Process

### Test Levels (L1–L4)
- **L1 Smoke**: API health, frontend accessible, login works — executed before any test campaign
- **L2 API**: Backend Gherkin scenarios executed via real HTTP calls
- **L3 UI**: Frontend Gherkin scenarios executed via Chrome MCP in the browser
- **L4 E2E**: Cross-layer scenarios (API + UI combined)

### Key Rules
- Tests must be executed **in the real application**, not just validated by reading code
- A test not executed in the app must be marked TO DO, never PASSED
- Smoke tests (L1) must pass before starting L2/L3/L4
- EF Core InMemory tests do NOT validate PostgreSQL LINQ translation — be aware of divergence
- Frontend async race conditions must be tested with real navigation (not just unit tests)

## LIMS Integration

AquaPlan integrates with **Limsophy** (by AAC Infotray) for laboratory data exchange:
- **Outbound**: sampling orders and metadata sent to Limsophy
- **Inbound**: analysis results received from Limsophy
- Integration layer in `AquaPlan.Infrastructure/Limsophy/`
- Use a dedicated `ILimsophyClient` interface for all LIMS communication
- All LIMS operations must be idempotent and resilient (retry with exponential backoff)
