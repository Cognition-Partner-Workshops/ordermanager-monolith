# Skill: Write an Architecture Decision Record (ADR)

## When to Use

Invoke this skill when the user asks to:
- Write, create, or draft an ADR
- Document an architecture decision
- Record a technical decision or design choice
- Add a new entry to `docs/adr/`

## Instructions

Follow every step below in order.

### Step 1 — Gather Context

Before writing, collect the following from the user (or infer from the conversation/task):

| Field | Description |
|-------|-------------|
| **Title** | Short noun phrase describing the decision (e.g., "Use PostgreSQL for Order Service") |
| **Status** | One of: `Proposed`, `Accepted`, `Deprecated`, `Superseded by ADR-XXXX` |
| **Context** | The forces at play — business drivers, technical constraints, team capabilities, deadlines, existing tech stack |
| **Decision** | The change being proposed or adopted — state it clearly and affirmatively ("We will ...") |
| **Consequences** | Positive, negative, and neutral outcomes that follow from the decision |

If the user has not provided enough detail, ask clarifying questions before proceeding. Do not fabricate architectural context.

### Step 2 — Determine the Next ADR Number

Run the following to find the next sequential number:

```bash
REPO_ROOT=$(git rev-parse --show-toplevel)
LAST=$(ls "$REPO_ROOT/docs/adr/" 2>/dev/null | grep -oP '^\d+' | sort -n | tail -1)
NEXT=$(printf "%04d" $(( ${LAST:-0} + 1 )))
echo "Next ADR number: $NEXT"
```

### Step 3 — Create the ADR File

Create the file at `docs/adr/{NEXT}-{slug}.md` where `{slug}` is a lowercase, hyphen-separated version of the title.

Use this template exactly:

```markdown
# ADR-{NEXT}: {Title}

**Date**: {YYYY-MM-DD}
**Status**: {Status}

## Context

{Describe the issue motivating this decision. Include technical and business forces,
constraints, and any relevant background. Reference existing ADRs or documentation
where applicable.}

## Decision

{State the decision clearly and affirmatively. Use "We will ..." language.
Be specific about technologies, patterns, or approaches chosen.}

## Alternatives Considered

### {Alternative 1 Name}

- **Description**: {Brief description}
- **Pros**: {List advantages}
- **Cons**: {List disadvantages}
- **Why rejected**: {Reason}

### {Alternative 2 Name}

- **Description**: {Brief description}
- **Pros**: {List advantages}
- **Cons**: {List disadvantages}
- **Why rejected**: {Reason}

{Add more alternatives as needed. Remove this section only if the user explicitly
states no alternatives were considered.}

## Consequences

### Positive

- {Positive outcome 1}
- {Positive outcome 2}

### Negative

- {Negative outcome or trade-off 1}
- {Negative outcome or trade-off 2}

### Neutral

- {Neutral observation 1}

## References

- {Link or reference to relevant documentation, RFCs, prior ADRs, etc.}
```

### Step 4 — Validate the ADR

After writing the ADR, verify:

1. **Title** is a concise noun phrase (not a sentence or question).
2. **Status** is one of the allowed values.
3. **Context** explains *why* this decision is needed, not just *what* was decided.
4. **Decision** uses affirmative "We will ..." language.
5. **Alternatives Considered** has at least one alternative (unless explicitly waived by the user).
6. **Consequences** includes at least one positive and one negative item — every decision has trade-offs.
7. The filename follows the `{NNNN}-{slug}.md` convention.
8. No placeholder text like `{describe ...}` remains in the final output.

### Step 5 — Update the ADR Index (if it exists)

If `docs/adr/README.md` exists, append a row to its table:

```markdown
| ADR-{NEXT} | {Title} | {Status} | {YYYY-MM-DD} |
```

If `docs/adr/README.md` does not exist, create one:

```markdown
# Architecture Decision Records

This directory contains the Architecture Decision Records (ADRs) for this project.

ADRs document significant architectural decisions along with their context and consequences.
For more information on ADRs, see [Michael Nygard's article](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions).

## Index

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| ADR-{NEXT} | {Title} | {Status} | {YYYY-MM-DD} |
```

### Step 6 — Commit and Report

Commit the new ADR file(s) with a message following this pattern:

```
docs: add ADR-{NEXT} — {Title}
```

Then inform the user the ADR has been created and provide the file path.

## Project-Specific Guidance

This repository (`OrderManager`) is a .NET 8 + Angular 17 monolith being decomposed into microservices. Common ADR topics include:

- Database strategy (shared SQLite vs per-service databases)
- Service decomposition boundaries (Orders, Products, Customers, Inventory)
- API gateway patterns and inter-service communication
- Authentication/authorization approach across services
- Event-driven vs synchronous communication
- Containerization and orchestration choices
- Frontend architecture (monolithic Angular app vs micro-frontends)
- CI/CD pipeline and deployment strategy
- Observability and monitoring approach
- Data migration strategy during decomposition

When writing ADRs for this project, reference the existing module structure:
- `src/OrderManager.Api/Controllers/` — API controllers per module
- `src/OrderManager.Api/Services/` — Business logic per module
- `src/OrderManager.Api/Models/` — Shared domain models
- `src/OrderManager.Api/Data/` — EF Core context and seed data
- `client-app/` — Angular 17 frontend
