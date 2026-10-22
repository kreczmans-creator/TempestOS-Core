# TempestOS v0.25.3 — Release Notes

A patch on `v0.25.2`, from the independent from-scratch test (2026-10-22).

## Fixed

- **P1: your data and Xero connection now survive an update.**
  - **Cause, part 1:** the installed app kept its data-location choice, and by default its data, in `%LOCALAPPDATA%\TempestOS`. That folder is Velopack's own install root, and `Setup.exe` clears it when it reinstalls. Every reinstall therefore forgot where the data lived and asked again.
  - **Cause, part 2:** `scripts/install-test-build.ps1` deliberately started a new data folder for every minor version (`C:\TempestOS-rc<minor>-data`).
  - **Fix:**
    - The choice and the default data now live in `%LOCALAPPDATA%\TempestOS-Data`, outside the install root.
    - On first launch, a choice recorded by an older build is carried over, and the old default data folder moves with it.
    - The install script now always uses `C:\TempestOS-data`. The first time, it is seeded with a copy of your newest `C:\TempestOS-rc*-data` folder, and the old folder is left untouched.
  - **Xero:** the client id and secret, tokens, organisation, contact links and settings are all stored under the data folder, so they now carry across updates too.
  - Code: `VelopackInstalledAppLocator.cs`, `PersistenceRootResolver.cs`, `App.cs`, `scripts/install-test-build.ps1`, `scripts/xero-demo-smoke.ps1`.
- **P3:** a refused save in Customers & Suppliers (for example, a duplicate code) now shows in red beside **Save organisation**, as well as at the top.
- **E7:** the Engineering Dashboard is left-aligned like every other page.
- **Runbooks Q3 / E3:** the steps now say that a quote carries its own revision. Re-exporting an unchanged quote stays at R1; editing a line makes it R2.

## Changed

- **Settings are split by who they belong to.**
  - **Settings, at the foot of the rail** (system-wide):
    - persistence root, backup and restore;
    - sign-off, evidence;
    - connector authorisation, Xero;
    - VAT, workflow, updates.
  - **Account settings, from your name in the header** (your own): job role, appearance, notifications and timesheets.
  - **Business → Organisation:** organisation identity and bank details, with their own Save.
  - Code: `ShellArea.SystemSettings`, `GlobalNavigationRail.cs`, `SettingsView.cs` (`SettingsPage`), `OrganisationView.cs`, `BusinessAreaView.cs`.

## Upgrading from v0.25.2 — do this once

- **Using the in-app update?** Nothing to do: your data location is carried over.
- **Installing `Setup.exe` over v0.25.2?** `Setup.exe` clears the old folder before v0.25.3 can move anything out of it.
  - If you kept the *default* data location, copy `%LOCALAPPDATA%\TempestOS\persistence-data` somewhere safe first, e.g. `C:\TempestOS-data`.
  - On first launch, the data-location dialog asks one last time. Pick the folder that holds your data.
  - After that it never asks again.
