using System.Text;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Library;

/// <summary>
/// Addendum B, S8 (ADR-0173): the governance a template can declare beyond an ordinary check — a feature flag that withholds it, a
/// competent-person attestation, and a second independent check. Nothing here names a particular calculator: a template opts in
/// through <see cref="CalcTemplate.FeatureFlag"/>, <see cref="CalcTemplate.RequiresAttestation"/> and
/// <see cref="CalcTemplate.RequiresSecondCheck"/>, and a template that sets none of them is governed exactly as before.
/// </summary>
public sealed partial class CalcInstanceService
{
    /// <summary>The shortest name or qualification the attestation accepts after trimming.</summary>
    public const int MinAttestationFieldLength = 3;

    /// <summary>Why <paramref name="templateReference"/> is switched off, or <see langword="null"/> (the catalogue's one message).</summary>
    private string? FeatureRefusal(string templateReference) => _catalogue.FeatureRefusal(templateReference);

    /// <summary>
    /// The keys a person is known by for the independence tests: a name or sign-in id compared without case, punctuation or spacing, so
    /// "Jane Smith", "jane.smith", "DOMAIN\jane.smith" and "jane.smith@example.org" all give the key "jane smith".
    /// </summary>
    internal static IReadOnlySet<string> PersonKeys(string? value)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(value))
            return keys;

        void Add(string text)
        {
            var sb = new StringBuilder();
            var gap = true;

            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                    gap = false;
                }
                else if (!gap)
                {
                    sb.Append(' ');
                    gap = true;
                }
            }

            var key = sb.ToString().Trim();

            if (key.Length > 0)
                keys.Add(key);
        }

        Add(value);

        var slash = value.LastIndexOf('\\');

        if (slash >= 0 && slash < value.Length - 1)
            Add(value[(slash + 1)..]);

        var at = value.IndexOf('@', StringComparison.Ordinal);

        if (at > 0)
            Add(value[..at]);

        return keys;
    }

    /// <summary>Whether two names or sign-in ids are the same person by <see cref="PersonKeys"/>.</summary>
    internal static bool SamePerson(string? a, string? b) => PersonKeys(a).Overlaps(PersonKeys(b));

    private static string Trimmed(CalcInstance instance, string symbol) =>
        CalcSheetTextRenderer.Clean(instance.Inputs.FirstOrDefault(i => string.Equals(i.Symbol, symbol, StringComparison.Ordinal))?.Text ?? string.Empty).Trim();

    /// <summary>
    /// Why the competent-person attestation of <paramref name="instance"/> does not stand, or <see langword="null"/> (also when the
    /// template asks for none). It needs a name, a qualification and a statement, and the person attesting cannot be anyone who
    /// created the calculation or set or changed any of its inputs.
    /// </summary>
    private static string? AttestationRefusal(CalcInstance instance, CalcTemplate template, string subject)
    {
        if (!template.RequiresAttestation)
            return null;

        var name = Trimmed(instance, CalcAttestation.NameSymbol);
        var qualification = Trimmed(instance, CalcAttestation.QualificationSymbol);
        var statement = Trimmed(instance, CalcAttestation.StatementSymbol);

        if (name.Length < MinAttestationFieldLength)
            return $"{subject} needs a competent-person attestation: enter the name of the competent person in '{CalcAttestation.NameSymbol}'.";

        if (qualification.Length < MinAttestationFieldLength)
            return $"{subject} needs a competent-person attestation: enter the attesting person's qualification in '{CalcAttestation.QualificationSymbol}'.";

        if (statement.Length < CalcAttestation.MinStatementLength)
            return $"{subject} needs a competent-person attestation: enter a statement of at least {CalcAttestation.MinStatementLength} characters in '{CalcAttestation.StatementSymbol}' saying what the person has reviewed.";

        foreach (var author in instance.Authors)
        {
            if (SamePerson(name, author))
                return $"{subject} needs an independent competent-person attestation: '{name}' created or edited this calculation ({author}); name a different competent person.";
        }

        return null;
    }

    /// <summary>
    /// What stops the second check of <paramref name="instance"/> standing at issue: none given, rejected, or by the same person as the
    /// first check or as an author. <see langword="null"/> when the template asks for none, or the countersignature stands.
    /// </summary>
    private static string? SecondCheckRefusal(CalcInstance instance, CalcTemplate template, string subject)
    {
        if (!template.RequiresSecondCheck)
            return null;

        return CountersignatureRefusal(instance.SecondCheck, instance.CheckedBy, instance.CheckerPrincipal, instance.Authors, subject);
    }

    private static string? CountersignatureRefusal(CalcCountersignature? second, string? firstName, string? firstPrincipal, IEnumerable<string> authors, string subject)
    {
        if (second is null)
            return $"{subject} needs a second, independent check (a countersignature by a different checker) before it can be issued.";

        if (!second.Accepted)
            return $"{subject} was rejected by its second checker ({second.Name}): {second.Statement}".TrimEnd() + " Revise it to correct and check it again.";

        if (SamePerson(second.Name, firstName) || SamePerson(second.Principal, firstPrincipal) || SamePerson(second.Name, firstPrincipal) || SamePerson(second.Principal, firstName))
            return $"{subject} needs two different checkers: the second check was signed by the person who made the first.";

        foreach (var author in authors)
        {
            if (SamePerson(second.Name, author) || SamePerson(second.Principal, author))
                return $"{subject} needs an independent second check: '{second.Name}' created or edited it ({author}).";
        }

        return null;
    }

    /// <summary>The first thing a template's extra governance refuses at Check: the attestation (the second check comes after the first).</summary>
    private static string? CheckGovernanceRefusal(CalcInstance instance, CalcTemplate template) =>
        AttestationRefusal(instance, template, $"Calculation '{instance.Name}'");

    /// <summary>Everything a template's extra governance demands before Issue: the attestation, then the second check.</summary>
    private static string? IssueGovernanceRefusal(CalcInstance instance, CalcTemplate template) =>
        AttestationRefusal(instance, template, $"Calculation '{instance.Name}'") ?? SecondCheckRefusal(instance, template, $"Calculation '{instance.Name}'");

    /// <summary>The checker line a sheet's header shows: the first checker, then the second when there is one that accepted.</summary>
    private static string? CheckerLabel(string? first, CalcCountersignature? second) =>
        second is { Accepted: true } ? $"{(string.IsNullOrWhiteSpace(first) ? "—" : first)}; second check: {second.Name}" : first;

    /// <summary>
    /// Records the second, independent check of a checked calculation whose template requires one (ADR-0173). The signed-in person must
    /// be known, must not have created the calculation or set or changed any of its inputs, and must not be the person who made the
    /// first check; the typed name is held to the same rules. A rejection is kept on the instance and refuses Issue, and a checked
    /// calculation with a rejected second check can be revised.
    /// </summary>
    public async Task<CalcActionResult> CountersignAsync(Guid instanceId, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome, CancellationToken cancellationToken = default)
    {
        if ((MissingText(checkerName, "A second checker name") ?? MissingText(checkerOrganisation, "The second checker's organisation")) is { } missing)
            return CalcActionResult.Refused(missing);

        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return CalcActionResult.Refused($"Template '{instance.TemplateReference}' is no longer registered.", instance);

        if (!entry.Template.RequiresSecondCheck)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' does not take a second check: its template does not require one.", instance);

        if (instance.Status != CalcInstanceStatus.Checked)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; it takes its second check after the first check and before issue.", instance);

        if (instance.SecondCheck is not null)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' already has a second check ({instance.SecondCheck.Name}).", instance);

        if (await StalenessAsync(instance, cancellationToken).ConfigureAwait(false) is { IsStale: true } stale)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is stale: {stale.Reasons[0]} Revise it before the second check.", instance);

        if (IndependenceRefusal(instance, $"'{instance.Name}'", "countersign it") is { } notIndependent)
            return CalcActionResult.Refused(notIndependent, instance);

        var principal = CurrentPrincipal();

        if (SamePerson(checkerName, instance.CheckedBy) || SamePerson(principal, instance.CheckerPrincipal) || SamePerson(principal, instance.CheckedBy) || SamePerson(checkerName, instance.CheckerPrincipal))
            return CalcActionResult.Refused($"'{instance.Name}' needs two different checkers: sign in as someone other than the first checker ({instance.CheckedBy}) to countersign it.", instance);

        foreach (var author in instance.Authors)
        {
            if (SamePerson(checkerName, author))
                return CalcActionResult.Refused($"'{instance.Name}' needs an independent second check: '{checkerName}' created or edited it ({author}).", instance);
        }

        var now = _time.GetUtcNow();
        var signature = new CalcCountersignature(CalcSheetTextRenderer.Clean(checkerName.Trim()), CalcSheetTextRenderer.Clean(checkerOrganisation.Trim()), CalcSheetTextRenderer.Clean(statement ?? string.Empty).Trim(), principal, now, outcome != CheckOutcome.Rejected);
        var updated = instance with { SecondCheck = signature, UpdatedAt = now };

        try
        {
            await _store.SaveInstanceAsync(updated, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException)
        {
            return CalcActionResult.Refused(ex.Message, await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) ?? instance);
        }

        return signature.Accepted
            ? CalcActionResult.Ok(updated)
            : CalcActionResult.Refused($"The second checker rejected the calculation; it stays checked but cannot be issued. {signature.Statement}".TrimEnd() + " Revise it to correct and check it again.", updated);
    }

    /// <summary>
    /// The pack form of <see cref="CountersignAsync"/>: a checked pack with a member whose template requires a second check takes the
    /// second signature on the pack, from a person who is none of the members' authors and not the pack's first checker.
    /// </summary>
    public async Task<CalcActionResult> CountersignPackAsync(Guid packId, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome, CancellationToken cancellationToken = default)
    {
        if ((MissingText(checkerName, "A second checker name") ?? MissingText(checkerOrganisation, "The second checker's organisation")) is { } missing)
            return CalcActionResult.Refused(missing);

        var pack = await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false);

        if (pack is null || pack.EvidenceId is null)
            return CalcActionResult.Refused($"No pack '{packId}' exists.");

        if (pack.Status != CalcInstanceStatus.Checked)
            return CalcActionResult.Refused($"Pack '{pack.Title}' is {pack.Status}; it takes its second check after the first check and before issue.");

        if (pack.SecondCheck is not null)
            return CalcActionResult.Refused($"Pack '{pack.Title}' already has a second check ({pack.SecondCheck.Name}).");

        var (members, gone) = await PackMembersAsync(pack, cancellationToken).ConfigureAwait(false);

        if (gone is not null)
            return CalcActionResult.Refused(gone);

        var needing = 0;

        foreach (var member in members)
        {
            if (FeatureRefusal(member.TemplateReference) is { } switchedOff)
                return CalcActionResult.Refused($"Pack member '{member.Name}': {switchedOff}");

            if (_catalogue.Find(member.TemplateReference)?.Template.RequiresSecondCheck == true)
                needing++;
        }

        if (needing == 0)
            return CalcActionResult.Refused($"Pack '{pack.Title}' does not take a second check: no member's template requires one.");

        var staleness = await StalenessForAsync(pack.ProjectId, members, cancellationToken).ConfigureAwait(false);

        foreach (var member in members)
        {
            if (staleness[member.Id] is { IsStale: true } stale)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is stale: {stale.Reasons[0]} Update it, reopen the pack and check it again.");

            if (IndependenceRefusal(member, $"Pack member '{member.Name}'", "countersign the pack") is { } notIndependent)
                return CalcActionResult.Refused(notIndependent);
        }

        var principal = CurrentPrincipal();

        if (!IsKnownPerson(principal))
            return CalcActionResult.Refused($"Pack '{pack.Title}' needs an independent second check: nobody is signed in, so there is no one to countersign it.");

        if (SamePerson(checkerName, pack.CheckerName) || SamePerson(principal, pack.CheckerPrincipal) || SamePerson(principal, pack.CheckerName) || SamePerson(checkerName, pack.CheckerPrincipal))
            return CalcActionResult.Refused($"Pack '{pack.Title}' needs two different checkers: sign in as someone other than the first checker ({pack.CheckerName}) to countersign it.");

        foreach (var author in members.SelectMany(m => m.Authors).Distinct(StringComparer.Ordinal))
        {
            if (SamePerson(checkerName, author) || SamePerson(principal, author))
                return CalcActionResult.Refused($"Pack '{pack.Title}' needs an independent second check: '{checkerName}' created or edited one of its calculations ({author}).");
        }

        var now = _time.GetUtcNow();
        var signature = new CalcCountersignature(CalcSheetTextRenderer.Clean(checkerName.Trim()), CalcSheetTextRenderer.Clean(checkerOrganisation.Trim()), CalcSheetTextRenderer.Clean(statement ?? string.Empty).Trim(), principal, now, outcome != CheckOutcome.Rejected);

        try
        {
            await _store.SavePackAsync(pack with { SecondCheck = signature, UpdatedAt = now }, pack.UpdatedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException)
        {
            return CalcActionResult.Refused(ex.Message);
        }

        return signature.Accepted
            ? new CalcActionResult(true, null, null)
            : CalcActionResult.Refused($"The second checker rejected the pack; it stays checked but cannot be issued. {signature.Statement}".TrimEnd() + " Reopen it to correct and check it again.");
    }
}
