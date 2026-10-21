# ADR-004: Concurrency via `xmin` token plus a partial unique index

- **Status:** Accepted, 2026-10-21
- **Deciders:** Product owner (PostgreSQL is specified in §4; this ADR fixes the concurrency mechanism)
- **Related:** spec §8, §11 (IT-06, IT-07, IT-10)

## Context

§8 requires that two competing ECOs based on the same released revision cannot both release as the next current revision, and warns against claiming concurrency safety without tests. The specification suggested "a product-level version token plus a transaction and a unique database constraint/index strategy".

With PostgreSQL (spec §4), there is a native mechanism for a version token that requires no schema column and no application bookkeeping: the `xmin` system column.

## Decision

Two independent defences, because they fail differently.

**1. Optimistic concurrency — `xmin` on `Product` and `EngineeringChangeOrder`.**

`Product` and `EngineeringChangeOrder` carry shadow `xmin` properties configured as concurrency tokens. Approve and release load and write the product row inside their transaction; every ECO edit or state transition writes the ECO row. If another transaction committed a conflicting write after the row was read, `xmin` has changed, EF throws `DbUpdateConcurrencyException`, and the endpoint maps it to `409 Conflict` with `conflictType: concurrency-conflict`. (`stale-base-revision` remains the separate, deterministic check that the ECO's base revision is still current.) This prevents stale draft edits, duplicate submissions, and competing approve/reject decisions from silently overwriting one another.

*Amended during Slice 2.* This ADR originally named Npgsql's `UseXminAsConcurrencyToken()` helper as the mechanism. That extension was **removed in Npgsql EF Core 10**, so the three calls it made are now written explicitly:

```csharp
builder.Property<uint>("xmin")
    .IsConcurrencyToken()          // original xmin goes in the UPDATE's WHERE clause
    .ValueGeneratedOnAddOrUpdate() // xmin stays out of the INSERT column list
    .HasDefaultValueSql("xmin");   // the system column supplies the value
```

The generated migrations confirm this is equivalent to the removed helper: `type: "xid", rowVersion: true, defaultValueSql: "xmin"`. The property is a shadow property, so `xmin` never appears in the domain model. The decision itself is unchanged — only the spelling of its implementation.

**2. Database enforcement — partial unique index.**

```sql
CREATE UNIQUE INDEX ux_bom_revisions_one_released_per_product
    ON bom_revisions (product_id)
    WHERE status = 'Released';
```

Even if the application logic were wrong, the database refuses a second current released revision. This is the invariant that actually matters, so it lives in the schema rather than in a code path that could be bypassed.

Related constraints, also in the schema:

- `bom_revisions (product_id, revision_code)` unique — no duplicate `C`.
- `eco_number` unique — no duplicate `ECO-2026-0042`.
- `(part_number)` unique per table (products, components), on the *stored* column — **amended
  during Slice 2.** This ADR originally said `upper(part_number)`, a functional index. EF Core
  cannot express one, and hand-editing the migration would leave the model snapshot disagreeing with
  the database. Case-insensitivity instead comes from the domain upper-casing at every entry point,
  so the plain index is equivalent in practice. Residual risk: a direct SQL insert bypassing the
  domain could introduce a case variant. Pinned by IT-09's two tests; see [part-number uniqueness notes](../DECISIONS.md#part-number-uniqueness).

## Release ordering (measured 2026-10-21)

A code review predicted that superseding the old revision and releasing the new one in a single
`SaveChanges` would violate `ux_bom_revisions_one_released_per_product`, because EF Core does not
guarantee the order of two updates to the same table. That was measured rather than debated.

**The prediction did not reproduce.** A released `B` and an approved `C` for one product, both
persisted, then both status changes in one `SaveChanges`: no exception, stably across five runs. EF
happened to issue the supersede first.

**That is still not a licence to batch it.** Three facts keep the ordered design:

1. EF Core guarantees ordering only for *dependent* commands. Two updates to one table have no such
   guarantee, so today's behaviour is an accident of batching that a patch release could change.
2. The constraint **cannot be deferred.** PostgreSQL allows `DEFERRABLE` only on table constraints,
   and a partial unique index is not one — so there is no schema-side escape hatch.
3. The failure, when it comes, would be a `23505` surfacing from a code path nobody thought could
   produce one. That is a bad shape for the demo's central invariant to fail in.

So the release use case (Slice 6) sequences **supersede → save → release → save inside one explicit
transaction**: two statements in a fixed order, kept atomic so a failure on the second rolls the
first back and the product is never left with no released revision.

Both halves are pinned: `IT11_ReleasingTheNewRevisionBeforeSupersedingTheOldOneViolatesTheIndex`
proves the constraint is live for the release-time shape (a second `Released` row, which IT-10 does
not cover — IT-10 conflicts on an `INSERT`), and
`IT11_OrderedTwoStepReleasePersistsAtomicallyInOneTransaction` proves the design that satisfies it.

The failed prediction is recorded deliberately. The next reader should see what was measured, not a
review that was quietly agreed with.

## Consequences

**Good**

- No hand-rolled version column, no "remember to increment the version" discipline, no trigger.
- The two defences are independent: the index does not depend on the token being read, and the token does not depend on anyone remembering the index.
- IT-06 (competing ECOs) and IT-10 (index refuses a second released revision) are real relational tests against the same engine the demo uses. Neither claim can be made from an in-memory provider, which is why §11 forbids one for these assertions.

**Costs / honesty requirements**

- `xmin` is PostgreSQL-specific. That is acceptable: spec §4 rules out a second provider, and no abstraction exists to pretend otherwise.
- `DbUpdateConcurrencyException` must be caught in exactly one place and mapped to a consistent `409`. Swallowing it, or retrying silently, would break the "return conflicts for stale data instead of silently rebasing" principle in §14.
- The application must not claim the index is the only defence, nor the token. Both are tested; IT-06 proves the user-visible behaviour, IT-10 proves the schema invariant.

**Rejected alternatives**

- *Application-only check ("read current revision, compare, then write"):* correct under a race only if the transaction takes a lock the check holds — which is exactly what the index gives us for free, with the database as witness.
- *Advisory locks per product:* real mutual exclusion, but serialises legitimately concurrent work and is harder to explain than a version token plus a constraint.
- *Serializable isolation for everything:* correct but disproportionate, and failure modes (serialization failures) need retry logic we would then have to build and test.
- *`rowversion`-style column:* SQL Server idiom; `xmin` achieves the same on PostgreSQL without a column.
