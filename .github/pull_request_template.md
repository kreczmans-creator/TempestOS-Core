<!--
  WP 11.1B — Pull Request Expectations. See
  docs/releases/v0.11.0/WP11.1B Engineering Workflow.md, "Pull Request
  Expectations," and docs/academy/06 Engineering Standards/
  Engineering Governance.md §9 (Decision Authority) for the full
  reasoning behind every section below. Delete this comment before
  submitting; keep every section that follows.
-->

## Work Package / Change

<!-- e.g. "WP 12.0B — Desktop Composition Root Decomposition Implementation" -->

## Summary

<!-- What changed and why, in a sentence or two. Link the Implementation
     Report / retrospective if one exists (docs/releases/vX.Y.0/). -->

## Review Gates (Engineering Governance §2)

- [ ] **Build Gate** — CI is green for this commit (`CI Gate` check),
      or a link to the run is included below.
- [ ] **Test Gate** — every test passes, including every pre-existing
      test — verified by the same CI run.
- [ ] **Technical Review Gate** — any non-obvious architectural
      decision, asymmetry, or deviation from an explicit brief
      requirement is justified in writing (below, or in a linked
      Implementation Report).

CI run: <!-- link -->

## Scope Confirmation

- [ ] No production code behaviour changed, **or** the change is
      described and justified above.
- [ ] No architecture changed, **or** an architecture document was
      updated in this same change.
- [ ] No ADR modified, **or** a new/updated ADR is included and linked.
- [ ] Documentation updated in the same change (`PROJECT_STATUS.md`,
      `WorkPackages.md`, Academy, governance registers — whichever this
      change's own subject matter touches, per Engineering Governance §4).

## Testing

<!-- What was tested, and how — new tests added, existing tests relied
     on, or (rarely, and stated explicitly) manual verification only. -->

## Product Approval

<!-- Left blank by the author. Engineering Governance §9: merging
     into `main` requires an explicit, per-occasion approval from
     Product Approval authority — this PR is not merged on Technical
     Review's sign-off alone. -->
