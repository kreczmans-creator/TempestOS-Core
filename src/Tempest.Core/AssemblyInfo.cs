using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tempest.Core.Tests")]

// WP 14.0A: lets the Companion's full-stack integration tests use the
// same internal TempestHostBuilder test seams Tempest.Core.Tests already
// relies on (explicit discovery/hosted-service candidate lists, so a
// real Host + Kestrel can be composed deterministically per test) — a
// build-visibility-only grant, the identical pattern Tempest.App applies
// for Tempest.Desktop.Tests.
[assembly: InternalsVisibleTo("Tempest.Companion.Tests")]
