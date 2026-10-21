## Phase 0: Requirements Discovery and Architecture Interview

Before implementing anything, act as a senior software architect and domain-driven design practitioner interviewing me as the product owner and technical decision-maker.

Your goal is to identify every meaningful product, domain, architectural, data-modeling, API, UX, testing, and operational decision that should not be made unilaterally by you.

Read `Forge-PLM-Spec.md` and inspect the existing repository first, including the Stitch-generated UI. Use both as context for your questions.

### 1. Interview me before making decisions

Ask questions that expose:
- Ambiguous or incomplete requirements.
- Conflicting requirements in the specification or generated UI.
- Domain rules that have multiple reasonable interpretations.
- Architectural trade-offs with meaningful consequences.
- Decisions that could cause expensive rework if made incorrectly.
- Assumptions that could compromise data integrity, historical correctness, concurrency, or the credibility of the demo.
- Scope decisions that could unnecessarily expand the project.

Do not ask me to decide trivial implementation details that have an obvious, low-risk default. Exercise engineering judgment for those and document your assumptions.

### 2. Areas you must investigate

Cover all relevant areas below. Adapt the questions to the actual repository and domain rather than mechanically asking every question.

**A. Product goals and demo**
- What is the primary success criterion: learning DDD, demonstrating PLM concepts, having a working demo, or all three?
- What must work end-to-end for the demo to be convincing?
- Which features should be excluded even if the generated UI suggests otherwise?
- What is the available implementation time, and what should be prioritized?

**B. Domain language and business rules**
- What exactly constitutes a Product versus a Component?
- Can a component itself have a BOM, or are BOMs single-level for now?
- Can the same component appear more than once in a BOM?
- Which lifecycle statuses are necessary, and what transitions are valid?
- What does approval authorize, and what does release mean?
- When does a revision become the current configuration?
- What does the effective date mean, and how should future-dated releases behave?
- Can an ECO contain multiple changes or affect multiple products?
- What happens if two ECOs change the same product concurrently?
- What should happen when a referenced component becomes retired?
- Which domain invariants must always hold?

**C. DDD and aggregate boundaries**
- Which entities should be aggregate roots, and why?
- Which objects must change atomically?
- Should BOM lines be owned by a BOM revision?
- Should ECO approval create a new revision inside the same transaction?
- Where should cross-aggregate validation live?
- Which value objects materially improve correctness?
- Are domain events necessary, or would direct application-service orchestration be simpler?
- Is the proposed modular monolith sufficient?
- Are separate projects justified, or are feature folders enough?

Do not ask me to choose patterns by name without explaining the practical trade-offs.

**D. Revisioning and historical integrity**
- How are revision identifiers assigned?
- Can approved revisions be superseded before release?
- Can a released revision ever be withdrawn or corrected?
- Must historical BOMs preserve component descriptions and part numbers as they existed at the time, or is retaining component references sufficient?
- Can multiple approved revisions exist simultaneously?
- How should stale ECOs be detected?
- What database constraints or concurrency mechanisms are needed to enforce these rules?

**E. Persistence and transactions**
- Which relational database should be used based on the repository and local environment?
- Which operations require transactions?
- What should happen if a transaction fails halfway through?
- How should optimistic concurrency be implemented?
- Which invariants need database constraints in addition to application validation?
- How should migrations and seed data work?
- What relational database tests are necessary to verify the important behavior?

**F. API and application architecture**
- Which use cases need dedicated endpoints?
- What should requests and responses contain?
- How should validation failures, invalid transitions, missing resources, and concurrency conflicts be represented?
- Should endpoints use controllers or Minimal APIs, considering the existing project?
- How should application services, domain logic, and EF Core persistence be separated?
- Are pagination, filtering, cancellation, and idempotency needed for the scoped use cases?
- Which abstractions add value and which would be premature?

**G. Frontend and interaction design**
- Which generated screens should be retained, simplified, or removed?
- What is the minimum navigation and user journey?
- Which tables, forms, and comparison views should be reusable components?
- How should the UI represent draft, submitted, approved, rejected, applied, and released states?
- Which actions require confirmation?
- How should API errors, loading, empty states, and stale data be handled?
- Should the frontend use the existing UI library, styling system, and routing setup?
- Which displayed values are genuine domain data versus decorative mock data?

**H. Testing and quality**
- Which rules deserve domain unit tests?
- Which workflows require integration tests against a real relational database?
- How should transaction rollback and concurrent ECO behavior be tested?
- What are the minimum build, lint, type-check, and test gates?
- What should the acceptance criteria be for each vertical slice?

**I. Developer experience and delivery**
- How should local configuration and database startup work?
- Should Docker Compose be used, or is a local database already available?
- What commands should run the API, frontend, migrations, seed data, and tests?
- What belongs in the README?
- What should be demonstrated during the interview?
- Which shortcuts are acceptable for a prototype, and which would undermine the domain model?

**J. Security and future integration boundaries**
- Is a fixed demo identity acceptable, and how should the code isolate it from future authentication?
- Which operations must record an actor and timestamp?
- What should be recorded in the audit trail?
- Should external ERP integration be explicitly excluded while preserving a sensible future integration boundary?
- Are there any security or data-integrity risks in the proposed shortcuts?

### 3. How to conduct the interview

Do not dump every question into one enormous message.

Instead:

1. Inspect the repository and specification.
2. Identify the highest-impact unresolved decisions.
3. Ask me one question at a time by default. Ask a small group together only when the decisions are closely related.
4. For each question, explain briefly why the decision matters.
5. Where useful, provide two or three plausible options, including the recommended default and its trade-off.
6. Let me answer in my own words. Do not force me to know DDD terminology.
7. Challenge inconsistent answers respectfully and ask follow-up questions when needed.
8. Keep track of decisions already made so you do not ask the same question again.
9. Distinguish decisions I explicitly made from assumptions you proposed and I accepted.
10. If I say "you decide," make a recommendation, explain it briefly, and record it as an AI-proposed default rather than a user-mandated requirement.

Do not turn this into a theoretical architecture exam. Every question must relate to this project or a meaningful engineering consequence.

### 4. Decision register

Maintain a decision register with these fields:

- ID
- Area
- Decision
- Status: Open, Confirmed, or Assumed
- Rationale
- Consequences
- Affected specification sections

Do not silently change a confirmed decision. If new information conflicts with one, bring it back to me.

### 5. Completion criteria

Do not begin implementation until:

- All high-impact ambiguities have been resolved.
- The core domain invariants and lifecycle transitions are explicit.
- Aggregate boundaries and transaction requirements are sufficiently clear.
- The intended demo scope is agreed.
- Remaining low-risk questions have documented defaults.
- I have reviewed and approved the consolidated decisions.

Then propose the necessary changes to `Forge-PLM-Spec.md` and create a concise architecture decision record for important choices.

Wait for my approval of the revised specification before implementing the first vertical slice.

### 6. Begin now

Start by inspecting the repository and current specification. Then tell me:
1. What you found.
2. The five most consequential unresolved decisions you identified.
3. Your first question.

Do not write application code yet.