# ADR-0001: Record Architecture Decisions

**Date**: 2026-05-12
**Status**: Accepted

## Context

As the OrderManager monolith is being decomposed into microservices, the team will make many architectural decisions over the coming months. These decisions — covering service boundaries, data ownership, communication patterns, and deployment strategy — need to be documented so that current and future team members can understand *why* the system is shaped the way it is.

Without a lightweight, version-controlled format for recording these decisions, knowledge lives only in meeting notes or developers' heads, making onboarding harder and increasing the risk of revisiting settled discussions.

## Decision

We will use Architecture Decision Records (ADRs), as described by Michael Nygard, to document all significant architectural decisions for this project. ADRs will be stored in `docs/adr/` and tracked in version control alongside the code they describe.

Each ADR will follow a consistent template containing: title, date, status, context, decision, alternatives considered, consequences, and references.

## Alternatives Considered

### Wiki Pages

- **Description**: Document decisions in a Confluence/Notion wiki.
- **Pros**: Rich formatting, easy to search, familiar to non-developers.
- **Cons**: Disconnected from the codebase, prone to going stale, harder to review in PRs.
- **Why rejected**: Keeping decisions close to the code ensures they evolve with it and are part of the review process.

### No Formal Documentation

- **Description**: Rely on commit messages and PR descriptions.
- **Pros**: Zero overhead.
- **Cons**: Decisions are scattered, hard to discover, and lack structured rationale.
- **Why rejected**: The decomposition effort involves many cross-cutting decisions that need a discoverable index.

## Consequences

### Positive

- Decisions are discoverable and version-controlled.
- New team members can understand the rationale behind the current architecture.
- PRs that add ADRs invite discussion before decisions are finalized.

### Negative

- Small overhead to write and maintain ADRs.
- Risk of ADRs going stale if statuses are not updated when decisions change.

### Neutral

- The team adopts a new lightweight process for documenting decisions.

## References

- [Michael Nygard — Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
- [ADR GitHub Organization](https://adr.github.io/)
