namespace Tempest.Core.People;

/// <summary>
/// One person in the People directory, as far as the "Switch person" act
/// (`WP 21.3B`) needs one: a display name, a role, an optional email, and
/// the identity id this person actually signs in as — the fourth field is
/// what makes a person choosable at all; a released person with none
/// cannot be switched to, since there is no stable id an audit row, a
/// "checked by" or a "signed off by" field could record for them.
/// </summary>
/// <param name="DisplayName">The name shown everywhere this person is named.</param>
/// <param name="Role">Free-text role or job title. <see langword="null"/> when not recorded.</param>
/// <param name="Email">This person's own email address. <see langword="null"/> when not recorded.</param>
/// <param name="IdentityId">
/// The stable identity id this person signs in as — the same shape
/// <see cref="Tempest.Core.Identity.ISessionPrincipal.IdentityId"/> carries
/// (an OS account SID on Windows, an OS user name elsewhere). <see langword="null"/>
/// for a person recorded in the directory with no known sign-in identity —
/// "Switch person…" never offers one of these, since there is nothing to
/// switch the session's own principal to.
/// </param>
public sealed record Person(string DisplayName, string? Role, string? Email, string? IdentityId);

/// <summary>
/// The people a consultancy's own engineers and principals are — read by
/// "Switch person…" (Settings → Principal, `WP 21.3B`) to list who a
/// second principal can become for the session.
/// </summary>
/// <remarks>
/// <para>
/// <b>A seam, not the real People directory.</b> `WP 20.10F` is building
/// the real, persistent People reference library (display name, role,
/// email, optional identity id) in parallel, on <c>release/v0.20.0</c>;
/// this Work Package's own brief names exactly this situation and its own
/// resolution: "design the second-principal sign-in to read People once it
/// lands... if it is not in your base when you need it, implement against
/// a small <c>IPeopleDirectory</c> seam with one in-memory implementation
/// and say so." `WP 20.10F` was not in this Work Package's own base
/// (`release/v0.21.0` at `122a34d0`, carrying only `WP 20.10A`/`20.10E`),
/// so that is exactly what this interface, and
/// <see cref="InMemoryPeopleDirectory"/>, are: once `WP 20.10F` merges, its
/// own real catalogue implements this interface (or this interface is
/// retired in favour of reading it directly) and nothing above this seam —
/// "Switch person…", the Evidence check refusal — needs to change at all.
/// </para>
/// </remarks>
public interface IPeopleDirectory
{
    /// <summary>
    /// Every released person carrying a known sign-in identity — exactly
    /// who "Switch person…" may offer. A person the directory holds but
    /// with <see cref="Person.IdentityId"/> unset, or not yet released, is
    /// never returned here.
    /// </summary>
    Task<IReadOnlyList<Person>> ListSwitchableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The one in-memory <see cref="IPeopleDirectory"/> implementation this
/// Work Package ships (see this interface's own remarks) — a plain,
/// process-lifetime list a composition root or a test seeds directly with
/// <see cref="Add"/>; nothing here is persisted, released, or reachable
/// from any Desktop surface that manages people, because none exists yet
/// on this branch.
/// </summary>
public sealed class InMemoryPeopleDirectory : IPeopleDirectory
{
    private readonly object _gate = new();
    private readonly List<Person> _people = [];

    /// <summary>Adds <paramref name="person"/> to this directory — the only way one is populated, since no Desktop surface manages People yet on this branch.</summary>
    public void Add(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        lock (_gate)
            _people.Add(person);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Person>> ListSwitchableAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Person> switchable = [.. _people.Where(p => !string.IsNullOrWhiteSpace(p.IdentityId))];
            return Task.FromResult(switchable);
        }
    }
}
