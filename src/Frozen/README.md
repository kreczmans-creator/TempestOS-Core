# Frozen layers

The plugin trust platform (`Tempest.Core.Plugins` — signing, the trust
store, trust tiers, capability enforcement, component principals, the
denied-type registry and assembly loading), the inbound REST API
(`Tempest.Core.Api`) and Licensing (`Tempest.Core.Licensing`) are frozen
out of the `v1.0` build by `ADR-0146`: there is no project file here, no
folder here is referenced by `src/TempestOS.slnx`, and nothing here
compiles, is tested, or is shipped. The code stands as it was at the
commit it was frozen at, compiles only against the `Tempest.Core` of that
commit, and is not maintained — every later change to `Tempest.Core` will
drift further from it, and that is expected rather than a defect. It is
kept, rather than deleted, because deleting it would throw away working
design that a paying client may one day ask for; it returns to the build
when one does — when a client asks for third-party plugins, for an
inbound API, or for licence enforcement — by being brought back as a real
project, re-pointed at the `Tempest.Core` of that day, and re-reviewed
against whatever the platform has become in the meantime. Plugin
*manifest discovery* is deliberately not here: it stays live, in place,
in `src/Tempest.Core/Plugins`, so the host keeps discovering and
recording what is in the plugin drop folder without loading, signing,
verifying, scoping or enforcing any of it. The matching tests are frozen
alongside, at `tests/Frozen/`.
