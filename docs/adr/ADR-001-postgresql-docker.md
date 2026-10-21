# ADR-001: PostgreSQL 18 in Docker Compose

- **Status:** Accepted, 2026-10-21
- **Deciders:** Product owner (explicit override of the agent's recommendation)
- **Related:** D-05, spec §4, §11, §15.1

## Context

The specification left the database open: "SQL Server or PostgreSQL; choose one". The machine has SQL Server Express installed (currently stopped), a stopped `mssql:2022` Docker image, and `sqllocaldb`. PostgreSQL is not installed.

The agent's recommendation was SQL Server, because it is already present and `sqlcmd` works. The owner instructed otherwise: PostgreSQL via Docker, latest stable version.

Version check on 2026-10-15: PostgreSQL **18** is the current stable major release (18.6). PostgreSQL 19 exists only as a release candidate, so it is not eligible for a demo that must start reliably.

## Decision

PostgreSQL 18, run through a committed `docker-compose.yml` with a named volume and a healthcheck. Integration and concurrency tests run against a dedicated database on the same container, created and dropped per test run. SQL Server is not supported and no provider abstraction exists to allow it later.

## Consequences

**Good**

- The database is a declared dependency of the repository, not of the machine. `docker compose up -d` is the documented start command.
- Tests run against the same engine as the demo, so transaction, partial-index and concurrency behaviour is genuinely exercised. The EF in-memory provider is explicitly forbidden for such claims.
- PostgreSQL gives us `xmin` concurrency tokens and partial unique indexes, which is exactly the strategy §8 needs (ADR-004).

**Neutral / costs**

- The installed SQL Server Express instance goes unused for this project. That is acceptable; it belongs to other work on this machine.
- `psql` is not installed on the host. Administration goes through `docker compose exec db psql …` — documented in the README.
- Pin the image tag to `postgres:18` (major-pinned) rather than `latest`, so a future major release cannot silently change behaviour mid-demo.

**Rejected alternatives**

- *SQL Server Express (local service):* works today, but the setup is not captured in the repository and won't reproduce on another machine.
- *SQL Server in Docker:* reproducible, but overridden by the owner's explicit instruction.
- *LocalDB:* zero-install, but deprecated, Windows-only, and behaves differently from a real server under concurrent connections — which is precisely the behaviour we claim to test.
- *Provider abstraction to support both:* pure cost. §2 forbids it and no requirement exists.
