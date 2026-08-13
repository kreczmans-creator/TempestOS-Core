# TempestOS Plugins

Reserved location for real, shipped plugin packages — a
`plugin.manifest.json` plus its loadable assembly, discovered and loaded
by the Plugin Manifest infrastructure (`PluginManifestDiscoveryService`,
`PluginAssemblyLoader`; `WP 4.2`, `ADR-0025`, `ADR-0026`) — parallel to
`src/Samples/` (in-repo sample modules) and `src/Templates/`
(scaffolding sources), neither of which is a plugin package itself.

This directory is, and has always been, deliberately empty: the
infrastructure that would discover and load a real plugin here is fully
implemented and tested, but no Work Package has yet placed a real plugin
package into it (`WP 4.3`'s Sample Module deliberately did *not*
package itself as a plugin — `RD-0015`, Rejected Designs Register — by
choice, not by omission). See `docs/governance/Engineering/Plugin
Register.md` for the authoritative, continuously-reviewed account of
this directory's status and its review trigger.

This file exists only so the directory itself is tracked by git, which
cannot record a directory with zero files in it — without it, a fresh
checkout silently loses `src/Plugins/` entirely, contradicting every
document (from `WP 2.1` onward) that describes it as an existing,
reserved, empty placeholder. Delete this file the moment a real plugin
package is added; a non-empty directory needs no marker.
