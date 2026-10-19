# ADR-0163: Engineering Splits into Disciplines and Shared Modules, and Structural, Electrical and Building Services Share One Attribute-Carrying Object

## Status

Accepted. Product Owner decision of 2026-10-19, after using `v0.25.0`:

- *"The disciplines should remain as we've had previous (mechanical/electrical/structural/HVAC/etc) and then the other bits form modules that sit beneath and can be used by anything above it."*
- *"Manufacturing is a discipline, as that will involve the likes of process improvements/process capabilities/processes."*
- *"Build those empty modules right now and start to flesh them out."*
- *"The cockpit is now redundant, as we're getting into here from projects which will have the status of everything."*
- *"We have three places with calculations… the record of issued sheets needs to be kept, but it could be tied into projects when completed and issued."*

This decision amends the `v0.24.0` Engineering navigation, in which the tree and the Structure tab list seven "disciplines". It also restores the discipline categories in `Capability Categories.md` (now `archive/docs-2026-09/governance/`).

## Context

`v0.25.0`'s Engineering tree listed Calculations, Documents, Evidence, Manufacturing, Mechanical, Requirements and Verification as *Disciplines*. It also had a *Modules* group holding Engineering Calculations, Calculators and Engineering Assets, and Reference data as a separate top-level node. Two problems followed:

- **The list mixed two kinds of thing.** Only Mechanical and Manufacturing are engineering domains. The other five are tools any domain uses.
- **Calculation work lived in three places.** Disciplines → Calculations held calculation objects. Modules → Engineering Calculations was the bracket-check register. Modules → Calculators held the generated-form calculators. Engineering Assets then added calculation packs and templates beside them.

Three of the domains `Capability Categories.md` named had never been started: Structural, Electrical and Building Services / HVAC. Every documents area also opened on the Engineering Cockpit. The project's own Overview now duplicates the Cockpit's status.

## Decision

**1. Disciplines are engineering domains.** The order is Mechanical, Structural, Electrical, Building Services / HVAC, Manufacturing (`EngineeringDisciplines.Disciplines`). Manufacturing stays a discipline: it will grow process capability and process improvement.

**2. Modules are shared tools beneath them.** The order is Calculations, Requirements, Verification, Documents, Evidence, Reference data (`EngineeringDisciplines.Modules` plus Reference data). The Engineering tree and a project's Structure tab both list Disciplines, then Modules. A discipline's navigator name is "<category> discipline"; a module's is "<category> module".

**3. Calculations is one module.**
- Its own node shows the project's calculation objects on the engineering surface.
- Its children are Calculators, Calculation register (the issued-sheet record, kept) and Packs and templates. Packs and templates is what was Modules → Engineering Assets.
- Tying an issued sheet into its project is the next step (follow-up below). The register is kept so nothing issued is lost.

**4. Structural, Electrical and Building Services / HVAC share one object.** `Tempest.Core.EngineeringDomain.DisciplineObject` (`IDisciplineObject`) is an ordinary engineering object: identifier, name, metadata, lifecycle, revisions, relationships and parent. It also carries an attribute dictionary in its type state. Each Kind declares its attributes in `Tempest.Workspace.Disciplines.DisciplineCatalogue`:

| Discipline | Kinds (Explorer folders) | Example attributes |
|---|---|---|
| Structural | Member, Connection, Foundation, Load case | Section, Material, Grade, Length (mm) |
| Electrical | Distribution board, Circuit, Load, Cable | Rating (A), Voltage (V), Size (mm²) |
| Building Services / HVAC | System, Zone, Equipment, Ductwork / pipework run | Floor area (m²), Design temperature (°C), Duty |

Attribute values are recorded exactly as typed. No design-code logic, safety factor or rating check is applied. This keeps the `Engineering Principles.md` boundary on discipline mathematics. One registration (`DisciplineWorkspaceRegistration`) wires every catalogue discipline:
- an Explorer area with one folder per Kind;
- a view and a Properties provider per Kind, listing every declared attribute, recorded or not;
- one shared set of handlers for Create, Rename, Edit, Delete (with undo), Move, Request review, Approve, Release and Set attribute;
- descriptors under each discipline's own command category and id prefix.

Adding a discipline or a Kind is a catalogue entry, not a new subsystem.

**5. The Cockpit leaves the documents area.** The documents area's permanent first tab becomes **Start**. It says how to open something and points to the project's Overview for status. `CockpitView` is still constructed and attached, unshown, for the coordinators that refresh it. Deleting it is a follow-up.

## Consequences

- The command bar's category filter is the union of disciplines and modules. A business category still stays out.
- Persistence needs no migration. New Kinds rehydrate through `DisciplineWorkspaceRegistration.RegisterRehydrators`. Existing Kinds are unchanged.
- Structural, Electrical and Building Services Kinds are not business-identifier enforced (`BusinessIdentifierScope.EnforcedKinds`): two members may share a name.
- These disciplines have no Copy/Duplicate yet; Move covers reparenting.

## Follow-ups

1. Tie a calculation sheet to its project when it is completed and issued, so it appears in that project's register as well as the module's.
2. Split Packs and templates. Packs and templates stay under Calculations; verification artefacts move to Verification; engineering evidence moves to Evidence.
3. Delete `CockpitView`, `EngineeringCockpit` and their read models once nothing refreshes them.
4. Give Manufacturing process capability (Cp/Cpk records) and process-improvement Kinds.
5. Link discipline objects into the Digital Thread: a circuit to its distribution board and cable, a member to its connections and load cases.
