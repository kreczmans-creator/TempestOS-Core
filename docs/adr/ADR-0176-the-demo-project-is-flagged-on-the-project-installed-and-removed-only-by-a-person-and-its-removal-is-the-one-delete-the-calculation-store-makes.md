# ADR-0174: The Demo Project Is Flagged on the Project, Installed and Removed Only by a Person, and Its Removal Is the One Delete the Calculation Store Makes

## Status

Accepted (v0.26.0, Addendum C, C-6). Builds on `ADR-0165` (Evidence governs a calculation), `ADR-0168` (links pin a revision and staleness is gated), `ADR-0172` (scan profiles are versioned and immutable) and `ADR-0074` (an engineering object is deleted by a flag, never erased). Adds no table, no collection and no migration: every row it writes is a row of an existing collection, written by the existing services.

## Context

Addendum C §C6 C-6 asks for one realistic fictional project holding every built chain, uses only invented data, marked DEMO on its project, its calculations and its exported sheets, installable and removable from a menu action and never installed into a real database by itself. Four questions follow.

1. Where does the DEMO mark live, so that no calculation's text carries it and a real calculation cannot?
2. How is the project made, and what stops an install from touching a project that is not the demonstration?
3. What does removal delete, given that issued records are immutable and an engineering object is never erased?
4. What does the person see, and when does anything run?

## Decision

1. **The flag is the project's own tag, and the sheet reads it.** The project is made through the same `EngineeringObjectFactory<Project>` the Projects area uses, with the tag `demo-project` in its metadata and the number `DEMO0-PRESS1`. `CalcDemoProject.IsDemo` is the one test of the flag; `CalcInstanceService` asks it when it builds a header and sets `CalcSheetHeader.Watermark` (a new optional last parameter, `null` on every real sheet). The text renderer prints it as the second line; the PDF prints it in the header band, as a notice line, in the footer of every page and in the file's title and subject. No template, input or assumption holds the word, so every sheet of a flagged project carries it, the issued sheet of a demo pack too, and no sheet of any other project does unless someone tags a project by hand (which only adds a mark). Every calculation, pack and Evidence record is also named "DEMO — …", so the lists say it.

2. **The demonstration is the project that carries the flag and the number.** Install refuses while a live project holds the number, whether it is the demonstration or a real project that took the number. Removal acts only on a live project that carries the flag *and* the number; one with either alone is "not the demo project" and is never touched. A project's id cannot be chosen (the factory mints it), so the number and the flag are the fixed identity.

3. **The install is the services, not a script.** Each pack is made by `InstantiatePackTemplateAsync` from its template, seeded from the template's own example inputs (`ExampleInputs`) with the specification the story names, the two synthetic cable tables given to the cable steps as plain, labelled inputs (no reference record is stored, so none can be licensed), and the scan-to-seal pair made by `CreateAsync`, `BindAsync`, `ApplyScanImportAsync` and `UpdateLinksAsync` from a 30-part synthetic table through two stored scan profiles whose ids start `demo-press-`. Nothing is checked or issued: a check names an independent second person. A failure part-way runs the removal on what was made.

4. **Removal is the one delete the calculation store makes, and it is narrow.** `CalcInstanceStore.DeleteDemonstrationRowsAsync` deletes in one transaction the instance and pack rows under the project's own key prefix and every version of the named `demo-press-` scan profiles, with their search-index rows; a profile id without the prefix is refused, and an issued or superseded row stops the transaction (nothing is deleted). The service refuses first, naming the issued calculation or pack, so an issued demonstration record is never deleted. The project and everything under it (the register entries and the Evidence) are then marked deleted with the platform's own soft delete, deepest first: the platform never erases an engineering object, its history stays, and the audit trail, which a removal never touches, gets one row for the install and one for the removal (`Demo.Installed`, `Demo.Removed`, with the counts). Rows of any other project are never read for deletion.

5. **A person asks, in the application's own dialog.** Settings → Demo project holds the two menu actions. Each asks in `ConfirmationDialog` and waits for the answer; a refusal or a No changes nothing; a removal is refused while the demonstration is the open project. `WorkspaceHost` builds the service and never runs it: nothing at start-up, on an empty database or on opening Settings installs anything.

## Consequences

- No new package, assembly, table, collection or migration. `CalcSheetHeader` gains one optional member (the golden sheets are unchanged: a null watermark prints nothing).
- A removed demonstration leaves its engineering objects as deleted tombstones (project, register entries, Evidence) and its audit rows; its calculation, pack and scan-profile rows are gone. Installing again makes a new project with new ids.
- A demonstration pack that has been issued cannot be removed; the message says which. The person can issue a copy in a project of their own instead.
- The synthetic cable tables and the steel section geometry of the steel-support example are the only figures not invented from nothing; the user page says so.

## Follow-ups

- The soft delete leaves the project in `IProjectDirectory.ListAsync` (it does not filter deleted projects, for projects the person deletes from the explorer too); surfaces that list projects from it should filter deleted ones.
