# Implementation decision notes

The product decisions and scope are in [the specification](Forge-PLM-Spec.md), especially §§15–16.
This page keeps implementation details that would otherwise be easy to misstate.

## Part-number uniqueness

Part numbers are trimmed and uppercased at the domain boundary. PostgreSQL has a plain unique
index on the stored value; case-insensitive uniqueness depends on all writes passing through the
domain normalization. A direct SQL write could bypass that rule. Tests cover normalization and
the database constraint separately.

## ECO numbering

The API assigns `ECO-{year}-{sequence}` numbers. A unique database constraint protects the
number, and creation retries a collision rather than asking the client to choose a number.

## Errors

One exception handler maps domain rule violations to 400 ProblemDetails and domain conflicts to
409. PostgreSQL unique violations and EF concurrency failures also map to 409, with a stable
conflict code. Endpoint handlers do not duplicate this mapping.

## Migrations and seed

The API applies migrations and the idempotent demo seed on startup when
`Database:ApplyOnStartup` is enabled. Compose enables it for the demo. A non-demo deployment
should apply migrations as a separate release step.

## Concurrency

Product `xmin` concurrency tokens and a partial unique index enforce release concurrency. The
database is PostgreSQL-specific by design. The implementation and measured release ordering are
documented in [ADR-004](adr/ADR-004-concurrency-xmin-partial-index.md); the integration tests run
against PostgreSQL rather than an in-memory provider.

## Short references in source comments

These compact labels come from the original decision register and remain in implementation
comments. Their durable requirements are stated in the spec or above.

| Label | Reference |
|---|---|
| `A-04` | Server-generated yearly ECO numbers; see [ECO numbering](#eco-numbering) |
| `A-07` | Product/component fields are immutable in v1; spec §16 |
| `A-11` | Migrations and idempotent seed run on startup when configured |
| `A-12` | Fixed GUIDs make demo seed application idempotent |
| `A-15` | UTC storage and local display; spec §3 |
| `A-16` | API serves the built SPA in demo mode; Vite proxy in development |
| `A-18` | `BomRevisionStatus.Draft` is unused in v1; spec §16 |
| `D-02c` | Protected demo path; spec §1 |
| `D-04a` | Approval and release are separate; spec §15, decision 7 |
| `D-04b` | Future effective dates are recorded labels, not scheduled actions; spec §15, decision 5 |
| `D-05` | PostgreSQL is the chosen database; spec §4 |
