# Engineering Knowledge Foundation v1.6.0

## Evidence-depth phase

The material breadth phase is complete enough to support the next engineering-data activity: converting candidate reference records into source-bound observations.

This release does **not** invent or silently add source citations. Instead it creates:
- an evidence schema;
- a steel-specific verification gate set;
- an observation-level sourcing queue for all 16 steel records;
- an explicit conflict-resolution policy.

### Steel verification boundary

The existing steel values remain `candidate_reference` and `screening_only`.

For each observation, the next source pass must establish:
**identity → condition → product form → thickness/section → property → temperature → test method → source locator**

Only after those checks can an observation move through the evidence state machine.

### Why this is the correct next step

The database already has breadth. The engineering value now comes from traceability and applicability: an engineer must be able to answer not just *what is the value?* but *for which exact material state, product form and condition, and from which source?*
