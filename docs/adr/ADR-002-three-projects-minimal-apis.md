# ADR-002: Three projects with Minimal APIs, plus a separate Vite SPA

- **Status:** Accepted, 2026-10-21
- **Deciders:** Product owner
- **Related:** D-01, D-03, D-06, spec §4

## Context

The specification's §4 offered a five-project layout (`Api`, `Domain`, `Application`, `Infrastructure`, `Web`) plus two test projects, and repeatedly instructed the implementer to "adapt to the generated scaffold".

**There is no scaffold.** The repository contains only the specification, this interview protocol, and static Stitch-generated mockups. No `.sln`, no `src/`, no `package.json`, no React application. Every decision the specification deferred to a scaffold is therefore still open, and the instruction to adapt has nothing to adapt to.

The primary goal (D-02) is an interview-ready demo in a few focused sessions, with PLM concepts demonstrated correctly. DDD pedagogy is explicitly not a goal.

## Decision

Three backend projects, Minimal APIs grouped by feature, and a separate Vite + React + TypeScript frontend:

```text
ForgePlm.sln
src/
  ForgePlm.Api/        endpoints, application services, EF Core, migrations, seed, served SPA
  ForgePlm.Domain/     aggregates, value objects, enums, domain errors — references nothing
tests/
  ForgePlm.Tests/      domain unit tests + PostgreSQL integration tests
web/                   Vite + React + TypeScript + Tailwind (compiled at build time)
```

Endpoints use Minimal APIs with an `IEndpointGroup`-style `MapXxx` extension per feature. Domain logic lives in aggregate methods; application services coordinate a use case and its transaction; endpoint delegates handle binding, validation of the HTTP shape, and status codes only.

## Consequences

**Good**

- Domain purity is a build-time guarantee: `ForgePlm.Domain` has no project reference to EF Core or ASP.NET Core. A domain rule that needs `DbContext` will not compile, which is the cheapest possible enforcement of §14.
- Three projects are explainable in one sentence in an interview. Five projects invite the question "what does Infrastructure contain that Api would not?" and the honest answer is "very little, at this scope".
- Minimal APIs remove controller ceremony without removing anything we need.

**Neutral / costs**

- The `Application` and `Infrastructure` layering is not separate. Application services live in `ForgePlm.Api` beside persistence. This is a deliberate trade: we accept co-location of application + infrastructure in exchange for not building an `Infrastructure` project whose only content is one `DbContext` and one seed class.
- The layering story an interviewer hears is "domain has no dependencies; everything else coordinates it", not "Clean Architecture with four layers". Say it that way.

**Rejected alternatives**

- *Five projects (spec §4):* the cleanest layering story, but the most scaffolding for this scope, and half the projects would be near-empty. Chosen against because D-02 says the demo, not the layering diagram, is the goal.
- *Single backend project with feature folders:* fastest, but domain logic and EF/HTTP concerns end up in the same folders, which makes the "domain rules live in the domain" claim unverifiable.

## Frontend notes

Tailwind is compiled at build time. The mockups load `cdn.tailwindcss.com` at runtime; that is acceptable for a mockup and unacceptable for an application that must start reliably offline in a demo room. In development the Vite dev server proxies `/api` to the API; for the demo the built SPA is served by the API host so the application runs on a single origin and CORS never enters the demo's risk list.
