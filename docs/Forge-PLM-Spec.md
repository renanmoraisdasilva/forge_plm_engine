# Forge PLM — Domain and Delivery Specification

**Status:** Phase 0 approved — revised implementation specification (2026-10-21)  
**Purpose:** A small, interview-ready Product Data Management (PDM) / Engineering Change Management prototype  
**Primary audience:** AI coding agents and developers  
**Architecture:** .NET modular monolith + React/TypeScript + relational database

---

## 1. Product goal

Build a small, credible product-configuration application that demonstrates core PDM concepts:

- Products and reusable components
- Bill of Materials (BOM) revisions
- Engineering Change Orders (ECOs)
- Controlled approval and revision creation
- Historical configuration preservation
- Auditability of meaningful domain actions

The application is a learning/demo prototype, not a production PLM platform. Prefer correct domain behavior and a small, understandable implementation over breadth.

### Demo scenario

Product `SERVER-100` (“Compute Server”) currently has released BOM revision `B`. It contains component `MB-020` (“Server Motherboard”), quantity 1.

An engineer creates an ECO to replace `MB-020` with `MB-021` (“Updated Server Motherboard”). The ECO is submitted, reviewed, and approved. The system creates a new BOM revision `C` while preserving revision `B` unchanged.

**Important:** Approval and effectivity are separate concepts. For this prototype, approval creates a new revision in `Approved` status. A separate explicit release action releases that revision and assigns an effective date. Do not automatically treat an approved ECO as released unless the user explicitly chooses to simplify this later.

---

## 2. Explicit scope

### In scope

1. Browse products.
2. View a product's current released BOM.
3. View BOM revision history and the component lines in each revision.
4. Browse engineering changes.
5. Create an ECO containing one or more proposed component changes.
6. Submit a draft ECO for review.
7. Approve or reject a submitted ECO, with reviewer comments/rejection reason.
8. On approval, create a new BOM revision from the current released BOM plus the approved changes.
9. Explicitly release the approved revision.
10. Record an audit trail for important state-changing actions.
11. Provide seeded data for the demo scenario.
12. Automated tests for domain invariants and key workflows.

### Out of scope

Do not implement these unless explicitly requested later:

- Authentication, authorization infrastructure, or multiple tenants
- Microservices, message brokers, event sourcing, CQRS frameworks, or distributed transactions
- CAD/3D viewer or CAD package management
- Supplier/AML, regulatory compliance, thermal analysis, or automated engineering gates
- ERP/MES integrations, external notifications, or retry/outbox infrastructure
- Effectivity by serial number, lot, plant, or configuration
- Multi-level BOM editing UI (the domain may represent component lines, but the initial workflow is a single-level BOM)
- Bulk import/export, CSV export, advanced analytics, charts, dashboards, or saved searches
- AI features
- Complex permissions or configurable approval matrices
- Generic workflow engine
- Generic repository/unit-of-work abstractions without a concrete need

Use one backend application and one relational database. Keep the code easy to navigate and explain.

---

## 3. Domain language

Use these terms consistently.

- **Product:** A sellable or buildable top-level item whose configuration is controlled.
- **Component:** A reusable part that may be included in one or more products.
- **BOM (Bill of Materials):** A specific product configuration expressed as a list of component references and quantities.
- **BOM revision:** An immutable snapshot of a product's BOM at a particular revision identifier.
- **BOM line:** One component and quantity in a particular BOM revision.
- **ECO (Engineering Change Order):** A controlled proposal to modify a product's BOM.
- **ECO line:** A proposed change to one component line, initially limited to replacing a component or changing quantity.
- **Approval:** A decision authorizing the ECO to proceed to revision creation.
- **Release:** An explicit action that makes an approved revision the current released configuration.
- **Effective date:** The date/time from which a released revision is considered applicable. For this prototype, store UTC timestamps and display in the user's local timezone.
- **Audit event:** An immutable record of a meaningful domain action, including actor, timestamp, action, and relevant reference/details.

Avoid using `ECN`, `CCB`, `baseline`, `production gate`, and other organization-specific terms as first-class concepts in the prototype. They can be mentioned in documentation as possible future extensions, but are not needed in the initial domain model.

---

## 4. Architecture and technology

### Backend

- .NET 10 / ASP.NET Core Web API
- C#
- Entity Framework Core with the Npgsql provider
- **PostgreSQL 18**, run through the committed `docker-compose.yml` (see ADR-001). No SQL Server support, no dual-provider abstraction.
- Minimal APIs grouped by feature. Controllers are not used.
- OpenAPI enabled in development
- xUnit for tests, including integration tests against the same PostgreSQL container

### Frontend

- React + TypeScript, built with Vite
- Tailwind CSS compiled at build time. The mockups load Tailwind from a CDN; the real application must not.
- Use the Stitch-generated pages as a **visual** reference (tokens, density, status chips, tables) — never as a source of domain data or scope.
- Keep API calls in a small typed client/service layer, separate from UI components.
- In development the Vite dev server proxies `/api` to the API. For the demo the built SPA is served by the API host so the application runs on a single origin (ADR-002).

### Architecture style

Use a **modular monolith organized by domain capability**. Feature boundaries (`Products`, `Components`, `BillOfMaterials`, `EngineeringChanges`, `Audit`) are enforced by folder structure inside the projects, not by separate deployables.

Keep domain rules in domain entities, value objects and domain services — never in React and never scattered across endpoint delegates. Application services coordinate use cases and persistence. Endpoints handle HTTP concerns only.

Do not add MediatR, AutoMapper, a generic repository layer, or a domain-event framework. There is no existing scaffold that uses them, and there is no documented reason to introduce them.

### Project layout

Three backend projects plus the frontend (ADR-002):

```text
ForgePlm.sln
src/
  ForgePlm.Api/           # Minimal API endpoints, application services, EF Core,
    Endpoints/            #   DbContext, migrations, seed, wwwroot (built SPA)
    Persistence/
    Seed/
  ForgePlm.Domain/        # Aggregates, value objects, enums, domain errors.
                          #   References nothing: no EF Core, no ASP.NET, no HTTP.
web/
  src/                    # Vite + React + TypeScript + Tailwind
tests/
  ForgePlm.Tests/         # Domain unit tests and PostgreSQL integration tests
docker-compose.yml        # PostgreSQL 18 + named volume + healthcheck
```

`ForgePlm.Domain` must compile with no dependency on EF Core or ASP.NET Core. If a domain rule needs `DbContext` to be expressed, the rule is in the wrong place.

---

## 5. Domain model and aggregate boundaries

Design around invariants and transaction boundaries, not database tables alone.

### 5.1 Product aggregate

**Root:** `Product`

Fields:
- `Id`
- `PartNumber` — required, normalized, unique
- `Name` — required
- `Category` — simple string or small enum for this prototype
- `LifecycleStatus` — `Draft`, `Active`, `Retired`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Rules:
- Part number is immutable after creation for the initial prototype.
- Part number must be unique, case-insensitively.
- Name cannot be blank.
- A retired product cannot receive a newly released BOM revision.
- Product name and category are immutable in v1: there is no update endpoint. Combined with an immutable part number, a reference alone describes history truthfully (see §16).
- Product does not own all BOM revision objects as in-memory child collections; revisions are queried separately to avoid loading unbounded history.

### 5.2 Component aggregate

**Root:** `Component`

Fields:
- `Id`
- `PartNumber` — required, normalized, unique
- `Name` — required
- `Category`
- `LifecycleStatus` — `Draft`, `Active`, `Retired`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Rules:
- Part number is immutable after creation for the initial prototype.
- Part number is unique case-insensitively.
- Name cannot be blank.
- A retired component cannot be added to a newly proposed or released BOM.
- Existing historical BOM revisions retain their reference to retired components.
- Component name, category and part number are immutable in v1: there is no update endpoint, which is what makes reference-only history truthful (see §16).

### 5.3 BOM revision aggregate

**Root:** `BomRevision`

A BOM revision is an immutable configuration snapshot for exactly one product. The aggregate contains its BOM lines. Treat each revision as a separate snapshot; do not update a released revision in place.

Fields:
- `Id`
- `ProductId`
- `RevisionCode` — sequential human-readable code, e.g. `A`, `B`, `C`
- `Status` — `Draft`, `Approved`, `Released`, `Superseded`
- `EffectiveAtUtc` — nullable until release
- `CreatedAtUtc`
- `CreatedBy`
- `SourceECOId` — nullable for initial seeded revision
- `Lines` — collection of `BomLine`

`BomLine` fields:
- `Id`
- `ComponentId`
- `Quantity` — positive decimal or integer; use decimal if fractional quantities are meaningful, otherwise positive integer for simplicity
- `Unit` — simple string such as `Each`
- Optional `ReferenceDesignator` is out of scope unless required by the UI

Rules:
- A revision belongs to exactly one product.
- Each component can appear at most once per revision in this initial model. If the same component is required in multiple distinct positions, that restriction can be relaxed later.
- Quantity must be greater than zero.
- Every component must exist and be active when a new revision is approved/released.
- An approved or released revision cannot have its lines edited.
- A released revision is immutable forever.
- Releasing a new revision marks the previous current released revision as `Superseded` in the same database transaction.
- At most one revision per product may be the current `Released` revision. `Superseded` revisions are historical and remain queryable.
- Revision codes are assigned sequentially per product. Use a simple documented scheme: initial revision `A`, then `B`, `C`, etc. Do not implement arbitrary alphabet rollover: if the next code would exceed `Z`, fail with a domain error rather than inventing a new scheme.
- `Draft` exists in the status enum for completeness only. Nothing in v1 creates a draft revision: revisions are created directly in `Approved` status by ECO approval.
- `EffectiveAtUtc` is metadata recorded at release. It never gates when a revision becomes current (see §8).
- If a revision is approved but not yet released, it is not the current released configuration.
- The product's current released revision is derived from revisions; avoid storing duplicate mutable `CurrentRevisionId` unless there is a justified consistency strategy.

### 5.4 Engineering Change Order aggregate

**Root:** `EngineeringChangeOrder`

Fields:
- `Id`
- `EcoNumber` — unique human-readable identifier, generated at creation as `ECO-{yyyy}-{NNNN}` where `{NNNN}` is a zero-padded sequence restarting each year (e.g. `ECO-2026-0042`). Enforce uniqueness with a database index; do not compute it from a mutable counter in application code alone.
- `Title`
- `ProductId`
- `BaseReleasedRevisionId` — released revision the proposal was created against
- `ReasonForChange`
- `ImpactAssessment`
- `RequestedEffectiveAtUtc`
- `Status` — `Draft`, `Submitted`, `Approved`, `Rejected`, `Applied`
- `CreatedBy`
- `CreatedAtUtc`
- `SubmittedAtUtc` — nullable
- `DecidedAtUtc` — nullable
- `DecidedBy` — nullable
- `ReviewerComment` — nullable
- `ApprovedRevisionId` — nullable; set when approval creates a new revision
- `Lines` — collection of `EngineeringChangeLine`

`EngineeringChangeLine` fields:
- `Id`
- `ChangeType` — initially `ReplaceComponent` or `ChangeQuantity`
- `TargetComponentId` — component being replaced/changed
- `ReplacementComponentId` — required only for replacement
- `NewQuantity` — required only for quantity change; for replacement, quantity defaults to the existing BOM line quantity unless the use case explicitly permits changing it
- Optional `Reason` for the individual line

Rules:
- ECO must reference an existing product and its current released base revision.
- At creation, capture `BaseReleasedRevisionId`; don't silently rebase the ECO later.
- An ECO must contain at least one change line before submission.
- Draft ECOs can be edited; submitted/approved/rejected/applied ECOs cannot be edited in this prototype.
- Only a `Draft` ECO can be submitted.
- Only a `Submitted` ECO can be approved or rejected.
- Rejection requires a nonblank reason.
- Approval requires a reviewer identity and may include a comment.
- An ECO may be approved only if its base revision is still the product's current released revision. If another ECO has released a newer revision in the meantime, reject the approval with a domain conflict and require the user to create a new ECO against the latest revision.
- Approval validates all proposed changes and creates an `Approved` BOM revision in the same database transaction as the ECO decision.
- Approval must not mutate any existing BOM revision.
- Release is a separate use case. Only the approved revision linked to the ECO may be released.
- On successful release, set the ECO to `Applied`, set the revision to `Released`, assign the effective timestamp, and mark the previous released revision `Superseded` atomically.
- A rejected ECO cannot be approved or released.
- The ECO's base revision and approved revision links must be retained for auditability.

### 5.5 Audit event

**Root:** `AuditEvent` (append-only record)

Fields:
- `Id`
- `OccurredAtUtc`
- `Actor`
- `Action` — e.g. `ProductCreated`, `ComponentCreated`, `EcoCreated`, `EcoSubmitted`, `EcoApproved`, `EcoRejected`, `RevisionReleased`
- `EntityType`
- `EntityId`
- `Details` — small JSON payload or structured text with relevant identifiers and before/after summary

Rules:
- Audit events are created in the same database transaction as the domain action they describe.
- Application code may insert audit events but must not edit or delete them.
- Do not implement event sourcing. Audit events are a simple historical record, not the source of truth.
- Do not log secrets or sensitive data.

---

## 6. Value objects and enums

Use value objects where they improve correctness without overengineering.

Recommended:
- `PartNumber` value object or normalized validated string type
- `Quantity` value object if useful for enforcing positivity
- Strongly typed IDs are not used. Ordinary `Guid` primaries (A-03 in `docs/DECISIONS.md`).

Enums:
- `ProductLifecycleStatus`: `Draft`, `Active`, `Retired`
- `ComponentLifecycleStatus`: `Draft`, `Active`, `Retired`
- `BomRevisionStatus`: `Draft`, `Approved`, `Released`, `Superseded`
- `EngineeringChangeStatus`: `Draft`, `Submitted`, `Approved`, `Rejected`, `Applied`
- `EngineeringChangeType`: `ReplaceComponent`, `ChangeQuantity`

Do not build a generic state machine framework. Implement explicit transition methods on the relevant aggregate, such as `Submit()`, `Approve()`, `Reject()`, and `MarkApplied()`.

---

## 7. Use cases and API contract

Use REST endpoints with clear request/response DTOs. Never expose EF entities directly.

All list endpoints accept `page` (1-based, default `1`) and `pageSize` (default `50`, maximum `200`) and return `{ items, total, page, pageSize }`.

The Components endpoints below are mandatory even though there is no Components screen: the Create/Edit ECO form needs a component picker to choose replacement parts.

### Products
- `GET /api/products?search=&status=&page=&pageSize=`
- `GET /api/products/{productId}`
- `POST /api/products`

### Components
- `GET /api/components?search=&status=&page=&pageSize=`
- `GET /api/components/{componentId}`
- `POST /api/components`

### BOM revisions
- `GET /api/products/{productId}/bom-revisions`
- `GET /api/products/{productId}/bom-revisions/current`
- `GET /api/bom-revisions/{revisionId}`
- `GET /api/bom-revisions/{revisionId}/lines`

### Engineering changes
- `GET /api/ecos?search=&status=&productId=&page=&pageSize=`
- `GET /api/ecos/{ecoId}`
- `POST /api/ecos`
- `PUT /api/ecos/{ecoId}` — draft only
- `POST /api/ecos/{ecoId}/submit`
- `POST /api/ecos/{ecoId}/approve`
- `POST /api/ecos/{ecoId}/reject`
- `POST /api/ecos/{ecoId}/release`

### Audit
- `GET /api/audit?entityType=&entityId=&page=&pageSize=`

For state-changing endpoints, use a request DTO with the actor identifier for this demo (e.g. `actor`), reviewer comments, rejection reason, or effective date as appropriate. Clearly document that production identity should come from authenticated claims, not a client-supplied actor field.

Use appropriate HTTP responses:
- `400` for malformed requests / request validation
- `404` for unknown IDs
- `409` for domain conflicts such as stale base revision or invalid state transition
- `201` for successful creation
- `200` for successful reads/actions with response bodies
- `204` for successful updates/deletes only when a response body is unnecessary

Use RFC 7807 `ProblemDetails` for every error. Return domain validation errors with actionable messages. Every `409` response carries a machine-readable `conflictType` (e.g. `stale-base-revision`, `invalid-transition`, `duplicate-part-number`, `revision-limit-reached`) so the UI can react without matching on prose.

Do not create endpoints for features that are out of scope.

---

## 8. Application use cases and transaction boundaries

Implement these as explicit application services: one class per use case, living in `ForgePlm.Api` beside the persistence it coordinates. The domain rules they call live in `ForgePlm.Domain` and must not reference the persistence layer.

### Create ECO
- Validate product exists and is active.
- Resolve current released BOM revision.
- Capture it as `BaseReleasedRevisionId`.
- Validate change line structure and referenced components.
- Save the ECO and `EcoCreated` audit event transactionally.

### Submit ECO
- Ensure ECO is in `Draft`.
- Ensure it has at least one line.
- Validate that target component(s) exist in the base revision and replacements exist and are active.
- Transition to `Submitted`, set timestamp, add audit event.

### Approve ECO
Within one database transaction:
1. Load the ECO and its lines.
2. Ensure status is `Submitted`.
3. Ensure `BaseReleasedRevisionId` is still the current released revision.
4. Validate every target line matches the base BOM and every replacement component is active.
5. Copy the base BOM lines into a new revision with the next revision code.
6. Apply all ECO lines to the copy.
7. Create the new revision in `Approved` status, linked to the ECO.
8. Set ECO to `Approved`, set reviewer/time and `ApprovedRevisionId`.
9. Add an `EcoApproved` audit event.
10. Commit all changes together.

If any step fails, no partial revision or approval record may remain.

### Reject ECO
Within one database transaction:
- Ensure status is `Submitted`.
- Require a nonblank rejection reason.
- Set `Rejected`, reviewer, decision time, and comment/reason.
- Add `EcoRejected` audit event.

### Release ECO
Within one database transaction:
1. Ensure ECO status is `Approved`.
2. Load the linked approved revision.
3. Ensure its status is `Approved`.
4. Ensure the base revision is still the current released revision.
5. Ensure an effective date was provided. Release always takes effect immediately; a future timestamp is stored as intent and displayed to the user as "Scheduled", never honoured by a background trigger (no scheduler exists in this prototype).
6. Mark the prior current revision `Superseded`.
7. Mark the new revision `Released` and set `EffectiveAtUtc`.
8. Mark the ECO `Applied`.
9. Add `RevisionReleased` audit event.
10. Commit.

If the base revision is stale at release time, return `409 Conflict`; do not silently overwrite the newer configuration.

### Concurrency

Use optimistic concurrency for the critical approve/release operations:

- `Product` carries an `xmin` concurrency token (PostgreSQL system column, exposed through EF Core with `UseXminAsConcurrencyToken`). Approve and release load and write the product row, so a competing release invalidates the losing transaction.
- The database additionally enforces the invariant directly: a partial unique index on `bom_revisions (product_id) WHERE status = 'Released'` makes a second current released revision impossible regardless of application logic.
- Handle `DbUpdateConcurrencyException` and map it to `409 Conflict`.
- Ensure that two competing ECOs based on the same released revision cannot both release successfully as the next current revision.

Prove this with the integration tests in §11 instead of asserting it in prose. Document any PostgreSQL-specific limitation rather than claiming concurrency safety without tests.

---

## 9. UI requirements

Use the Stitch screenshots as visual reference, but remove unnecessary detail. Implement only these screens:

**Navigation:** a minimal sidebar with exactly two entries — **Products** and **Engineering Changes**. The mockups' Overview, Bill of Materials and Components entries do not exist as pages; BOM content lives in the Product detail tabs.

**Visual language to keep:** dense tables, status chips, monospaced part numbers, the ECO lifecycle stepper, the before/after BOM comparison, the audit timeline, the compact Inter + JetBrains Mono typography from `mockups/precision_industrial_engineering/DESIGN.md`.

**Delete every mockup element with no domain source:** KPI tiles and percentages, Export CSV, Batch Audit, CAD/Spec-sheet viewer, supplier (AML) and compliance (RoHS/REACH) cards, CCB authority and "ISO/IEC 15288" checkbox, cryptographic signature badges, ERP/MES notes, notifications, user avatar, global search bar, pagination controls beyond the standard pager, and any invented identifiers (e.g. `CCB-REQ-77401`, `ASY-9940-02`).

### A. Products list
- Search and a simple lifecycle status filter
- Table: part number, name, category, current released revision, lifecycle status
- Row click opens product detail
- No `Create Product` UI in v1. `POST /api/products` exists for API completeness and seeding only.

### B. Product details
- Header with name, part number, lifecycle status, current released revision
- Tabs: BOM, Revisions, Related Changes (Related Changes can be a simple filtered list)
- BOM table with component, part number, quantity, unit, revision/status
- Revision history table
- `Create ECO` button

### C. Engineering Changes list
- Search and status filter
- Table: ECO number, title, product, status, created date
- Row click opens ECO detail
- `Create ECO` action

### D. ECO details
- Header with ECO number, title, product, status
- Reason and impact assessment
- Before/after comparison of proposed BOM change
- Workflow status and audit timeline
- Actions appropriate to status: Submit, Approve, Reject, Release
- Release action visible only when ECO is approved
- Approve and Release carry the current actor from the actor switch and show it in the confirmation dialog

### E. Create/Edit ECO form
- Title, affected product, change type, reason, impact assessment, requested effective date
- Existing component and replacement component / new quantity
- Save draft and submit actions

Do not implement the overview dashboard, detailed component inspector, CAD views, supplier cards, compliance widgets, analytics, or bulk actions in the first version.

**Actor switch:** a small persistent control in the app header with two seeded identities — `Alex Morgan` (engineer/author) and `Jordan Lee` (reviewer). It is not authentication: the selected name travels in the request body and is read through an `IActorProvider` boundary so real authenticated claims can replace it later (§7, ADR-005).

UI behavior:
- Use loading, empty, success, and error states.
- Disable actions that are invalid for the current status.
- Ask for confirmation before approve, reject, or release.
- Display API validation errors in context.
- Do not hardcode fake status changes in the frontend once APIs exist.
- Use consistent status labels and dates across screens.

---

## 10. Seed data

Create deterministic seed data for the demo.

### Components
- `CPU-001` — Xeon Processor — category `Processor` — Active
- `CPU-002` — Intel Xeon Silver 12-Core Processor — category `Processor` — Active
- `CPU-003` — Intel Xeon Gold 24-Core Processor — category `Processor` — Active
- `CPU-004` — AMD EPYC 32-Core Processor — category `Processor` — Active
- `MB-020` — Server Motherboard — category `Motherboard` — Active
- `MB-021` — Updated Server Motherboard — category `Motherboard` — Active
- `MB-022` — Dual-Socket Xeon Server Motherboard — category `Motherboard` — Active
- `RAM-032` — 32GB Memory Module — category `Memory` — Active
- `RAM-064` — 64GB DDR5 ECC Registered Memory Module — category `Memory` — Active
- `RAM-128` — 128GB DDR5 ECC Registered Memory Module — category `Memory` — Active
- `SSD-100` — Enterprise SSD 1TB — category `Storage` — Active
- `SSD-200` — Enterprise NVMe SSD 2TB — category `Storage` — Active
- `SSD-400` — Enterprise NVMe SSD 4TB — category `Storage` — Active
- `NIC-010` — 10Gb Network Adapter — category `Networking` — Active
- `NIC-025` — Dual-Port 25GbE Network Adapter — category `Networking` — Active
- `NIC-100` — Dual-Port 100GbE Network Adapter — category `Networking` — Active
- `FAN-005` — Cooling Fan Assembly — category `Thermal` — Active
- `FAN-006` — Hot-Swap High-Pressure Cooling Fan — category `Thermal` — Active
- `FAN-007` — Redundant High-Performance Cooling Fan — category `Thermal` — Active

### Product
- `SERVER-100` — Compute Server — category `Server` — Active

### Initial BOM
- Revision `B` — `Released`
- CPU-001 quantity 2, unit `Each`
- MB-020 quantity 1, unit `Each`
- RAM-032 quantity 8, unit `Each`
- SSD-100 quantity 2, unit `Each`
- NIC-010 quantity 1, unit `Each`
- FAN-005 quantity 4, unit `Each`

Seed revision B as the current released baseline, even if earlier revisions are omitted from this compact demo. Document that revision B is the starting snapshot for the prototype.

For a useful demo, seed one draft ECO or allow the user to create it through the UI. If seeding an ECO, make its status and associated data internally consistent.

Seed data must be idempotent: repeated application starts or seed execution must not duplicate records. Use **fixed GUID identifiers** for every seeded record so re-application is a no-op rather than a duplicate check.

Seed the two demo actors used by the actor switch: `Alex Morgan` (engineer/author) and `Jordan Lee` (reviewer). Seed data may be authored as `Alex Morgan` throughout, so the demo's first visible act is switching to `Jordan Lee` to approve.

---

## 11. Testing requirements

Write tests for domain behavior and application workflows.

Minimum domain tests:
- Blank part numbers and names are rejected.
- BOM line quantity must be positive.
- Released BOM revision cannot be modified.
- Only a draft ECO can be submitted.
- Only a submitted ECO can be approved or rejected.
- Rejection requires a reason.
- Approved ECO cannot be approved again.
- ECO cannot be approved if its base revision is no longer current.

Minimum application/integration tests:
- Creating an ECO captures the current released revision.
- Approval creates a new revision with the requested change applied.
- The old released revision remains unchanged after approval.
- Release supersedes the previous revision and releases the new revision atomically.
- Rejecting an ECO does not create a new BOM revision.
- Two competing ECOs based on the same revision cannot both release successfully.
- Failed approval/release rolls back all associated changes.

Prefer a small number of meaningful tests over broad, superficial coverage.

**Integration tests must run against real PostgreSQL.** The EF Core in-memory provider does not model transactions, partial indexes or concurrency tokens, and must not be used to support any claim about them. `ForgePlm.Tests` opens a dedicated database on the Compose PostgreSQL container, created and dropped per test run, so tests are honest about relational behaviour.

---

## 12. Definition of done

The first version is complete when:

1. The app starts locally with documented commands.
2. Seed data loads idempotently.
3. The products list and product detail screens load data from the API.
4. A user can create and submit an ECO.
5. A submitted ECO can be approved or rejected.
6. Approval creates a new approved BOM revision and preserves the old revision unchanged.
7. An approved revision can be released separately.
8. Releasing the new revision supersedes the old revision and records an effective date.
9. Invalid state transitions return clear errors.
10. Audit events are persisted for the key lifecycle actions.
11. Automated tests cover the critical domain rules and workflow.
12. README documents architecture, key domain decisions, setup, seed data, API routes, test commands, and known limitations.

---

## 13. AI implementation workflow

Implement incrementally. Do not generate the entire application in one pass.

Before coding:
1. Inspect the existing repository and generated UI.
2. Identify the current framework, routes, styling system, and any existing components.
3. Summarize the proposed implementation plan and any deviations from this spec.
4. Identify unresolved design questions. Prefer the simplest choice consistent with this spec.

Then work in small vertical slices, in this order:

1. `ForgePlm.Domain`: aggregates, enums, domain errors, plus domain unit tests.
2. Persistence: `ForgePlmDbContext`, entity configurations, constraints, migrations, idempotent seed, `docker-compose.yml`.
3. Product and BOM read endpoints.
4. ECO creation and submission endpoints.
5. Approval and revision creation transaction.
6. Rejection and explicit release, plus the competing-ECO concurrency test.
7. The React app: shell, actor switch, Products list, Product detail, ECO list, ECO detail, Create/Edit ECO.
8. Integration tests, error states, and README.

For each slice:
- State the files to be created or changed.
- Implement the smallest complete slice.
- Run build and relevant tests.
- Fix failures before proceeding.
- Summarize behavior implemented, tests run, and remaining work.
- Do not claim tests passed unless they were actually run.

When code and this specification conflict, do not silently ignore the conflict. Explain it and propose a focused update to the spec. If a requirement is ambiguous, choose the simplest behavior that preserves the stated invariants and document the decision.

---

## 14. Engineering principles

- Prefer explicit domain methods over public setters that permit invalid states.
- Keep domain logic independent of EF Core and HTTP.
- Keep controllers/endpoints thin.
- Use DTOs at API boundaries; never serialize tracked entities directly.
- Use database constraints for uniqueness and other invariants where practical, in addition to application validation.
- Use UTC for persisted timestamps.
- Make state transitions explicit and testable.
- Preserve history instead of overwriting released data.
- Use transactions for approval and release operations.
- Return conflicts for stale data instead of silently rebasing changes.
- Avoid premature abstractions and unnecessary dependencies.
- Keep the implementation small enough that a developer can explain every important decision in an interview.

## 15. Decisions resolved in Phase 0

These were open in the first draft and were settled during the requirements interview on 2026-10-21. The full register, with rationale and consequences, lives in `docs/DECISIONS.md`; the load-bearing choices have ADRs in `docs/adr/`.

1. **Database:** PostgreSQL 18, run through the committed `docker-compose.yml`. The same container hosts a dedicated integration-test database. No SQL Server support. *(Owner decision, overriding the "SQL Server or PostgreSQL" option.)*
2. **Revision identifiers:** `A`, `B`, `C`, … assigned per product. The code fails with a domain error rather than rolling over after `Z`.
3. **Quantity:** positive integers. No fractional quantities are needed for this prototype.
4. **Identity:** no authentication. A two-person actor switch (`Alex Morgan`, `Jordan Lee`) in the UI sends the actor in the request body and is read through an `IActorProvider` boundary, so authenticated claims can replace it later without touching the domain.
5. **Effectivity:** one UTC timestamp recorded at release. Release always takes effect immediately; a future timestamp is stored as intent and displayed as "Scheduled". No scheduler, no serial/lot effectivity.
6. **BOM depth:** single-level BOM, with the model left open to hierarchical expansion.
7. **Release policy:** a revision is approved first, then explicitly released. This is intentional and must remain consistent across the API and UI.
8. **Code structure:** three backend projects (`ForgePlm.Api`, `ForgePlm.Domain`, `ForgePlm.Tests`) with Minimal APIs, plus a separate Vite/React/TypeScript frontend. *(ADR-002.)*
9. **Concurrency:** `xmin` token on `Product` plus a partial unique index guaranteeing one `Released` revision per product; conflicts map to `409`. *(ADR-004.)*

## 16. Known limitations and shortcuts

These are deliberate, and the README must list them:

- **No DELETE anywhere.** A draft ECO cannot be cancelled or removed. A rejected ECO is the terminal state for an idea that did not proceed.
- **No update endpoints for products or components.** Part numbers, names and categories are immutable in v1. This is what makes reference-only history truthful; if editing is ever added, historical snapshots become mandatory.
- **Lifecycle statuses are modelled but mostly unreachable.** Product and component `Retired` are never produced by any endpoint in v1.
- **`BomRevisionStatus.Draft` is unused.** Revisions are created directly in `Approved`.
- **Single-level BOM only**, and revision codes stop at `Z`.
- **No authentication, authorization or multi-tenancy.** The actor field is client-supplied and demo-only; never present it as a security control.
- **No ERP/MES integration, notifications or outbox**, though the audit trail and ECO records are shaped to remain useful to one later.
