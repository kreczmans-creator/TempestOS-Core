using System.Security.Cryptography;
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

    /// <summary>The shortest statement a countersignature or a rejection carries.</summary>
    public const int MinSignatureStatementLength = 20;

    /// <summary>The shortest self-check statement after trimming (DA-12): enough to say what was looked at, not a word.</summary>
    public const int MinSelfCheckStatementLength = 20;

    /// <summary>The longest self-check statement kept (DA-12); a longer one is refused, not cut, so what is printed is what was written.</summary>
    public const int MaxSelfCheckStatementLength = CalcSheetStandard.MaxSelfCheckLength;

    /// <summary>The words of the box the preparer ticks to check their own work (DA-12).</summary>
    public const string SelfCheckCaption = "Self-check (no second person available)";

    /// <summary>The audit action written when a preparer checks their own calculation (DA-12).</summary>
    public const string SelfCheckedActionName = "Calc.SelfChecked";

    /// <summary>The audit action written when a preparer checks their own calculation pack (DA-12).</summary>
    public const string PackSelfCheckedActionName = "Calc.PackSelfChecked";

    /// <summary>Writes the audit row of a self-check: who, when, which calculation (or pack) and the statement they gave. Nothing is written where no audit recorder is composed.</summary>
    private async Task RecordSelfCheckAsync(string action, CalcSelfCheck selfCheck, Dictionary<string, string> detail, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;

        detail["Checker"] = selfCheck.Name;
        detail["CheckerPrincipal"] = selfCheck.Principal;
        detail["Statement"] = selfCheck.Statement;
        await _audit.RecordAsync(action, detail, cancellationToken).ConfigureAwait(false);
    }

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

    /// <summary>Whether two names or sign-in ids are the same person by <see cref="PersonKeys"/> alone.</summary>
    internal static bool SamePerson(string? a, string? b) => PersonKeys(a).Overlaps(PersonKeys(b));

    /// <summary>
    /// The keys of a name or sign-in id with the display name the principal directory gives it: on Windows a principal id is the account SID, which
    /// no typed name resembles, so the directory's account name is compared too (a person is not independent of themselves because the id is a SID).
    /// </summary>
    private IReadOnlySet<string> KeysOf(string? value)
    {
        var keys = new HashSet<string>(PersonKeys(value), StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(value) && _principals is not null)
        {
            try
            {
                keys.UnionWith(PersonKeys(_principals.Describe(value)));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Security.SecurityException)
            {
                // A directory that cannot answer adds no key; the id's own keys still apply.
            }
        }

        return keys;
    }

    /// <summary>Whether two names or sign-in ids are the same person, with the principal directory's display names as well as the text.</summary>
    internal bool Same(string? a, string? b) => KeysOf(a).Overlaps(KeysOf(b));

    /// <summary>A hash of the inputs, so an attestation made on them is voided by any later change.</summary>
    internal static string HashInputs(IReadOnlyList<CalcInputValue> inputs) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CalcInstanceStore.Serialise(inputs.OrderBy(i => i.Symbol, StringComparer.Ordinal).ToList()))))[..32];

    /// <summary>The first checker's sign-in and typed name (and the second's) of an instance, with those of its pack when it is issued with one.</summary>
    private static IEnumerable<(string? Principal, string? Name)> CheckersOf(CalcInstance instance, CalcPack? pack)
    {
        yield return (instance.CheckerPrincipal, instance.CheckedBy);

        if (instance.SecondCheck is { } own)
            yield return (own.Principal, own.Name);

        if (pack is not null)
        {
            yield return (pack.CheckerPrincipal, pack.CheckerName);

            if (pack.SecondCheck is { } second)
                yield return (second.Principal, second.Name);
        }
    }

    /// <summary>
    /// Why the competent-person attestation of <paramref name="instance"/> does not stand, or <see langword="null"/> (also when the template asks
    /// for none). It needs an attestation made through <see cref="AttestAsync"/> by a signed-in person, on the inputs as they now are, naming a person,
    /// a qualification and a statement; and that person (by sign-in, by display name and by the name they gave) is none of the calculation's authors
    /// and none of its checkers.
    /// </summary>
    private string? AttestationRefusal(CalcInstance instance, CalcTemplate template, string subject, CalcPack? pack = null)
    {
        if (!template.RequiresAttestation)
            return null;

        if (instance.Attestation is not { } attestation)
            return $"{subject} needs a competent-person attestation: a competent person who is not its author signs in and attests it (Attest), giving their name, qualification and a statement.";

        if (!string.Equals(attestation.InputsHash, HashInputs(instance.Inputs), StringComparison.Ordinal))
            return $"{subject}: its inputs changed after the competent-person attestation by {attestation.Name}; attest it again.";

        if (attestation.Name.Length < MinAttestationFieldLength || attestation.Qualification.Length < MinAttestationFieldLength || attestation.Statement.Length < CalcAttestation.MinStatementLength)
            return $"{subject}: the competent-person attestation is incomplete (a name, a qualification and a statement of at least {CalcAttestation.MinStatementLength} characters are needed).";

        if (!IsKnownPerson(attestation.Principal))
            return $"{subject}: the competent-person attestation was not made by a signed-in person.";

        foreach (var author in instance.Authors)
        {
            if (Same(attestation.Principal, author) || Same(attestation.Name, author))
                return $"{subject} needs an independent competent-person attestation: '{attestation.Name}' created or edited this calculation ({Describe(author)}); another competent person must attest it.";
        }

        foreach (var (principal, name) in CheckersOf(instance, pack))
        {
            if (principal is null && name is null)
                continue;

            if (Same(attestation.Principal, principal) || Same(attestation.Principal, name) || Same(attestation.Name, principal) || Same(attestation.Name, name))
                return $"{subject} needs an independent competent-person attestation: '{attestation.Name}' is also one of its checkers ({name ?? Describe(principal)}).";
        }

        return null;
    }

    /// <summary>The name to show for a sign-in id in a refusal: the directory's, else the id.</summary>
    private string Describe(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "unknown";

        try
        {
            return _principals?.Describe(id) ?? id;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return id;
        }
    }

    /// <summary>Why the person about to check or countersign <paramref name="instance"/> is also its attester, or <see langword="null"/>.</summary>
    private string? AttesterIsSigningRefusal(CalcInstance instance, CalcTemplate template, string signerName, string subject)
    {
        if (!template.RequiresAttestation || instance.Attestation is not { } attestation)
            return null;

        var principal = CurrentPrincipal();

        if (Same(principal, attestation.Principal) || Same(signerName, attestation.Name) || Same(principal, attestation.Name) || Same(signerName, attestation.Principal))
            return $"{subject} needs checkers independent of its competent-person attestation: sign in as someone other than the attester ({attestation.Name}).";

        return null;
    }

    /// <summary>
    /// What stops the second check of <paramref name="instance"/> standing at issue: none given, rejected, or by the same person as the
    /// first check or as an author. <see langword="null"/> when the template asks for none, or the countersignature stands.
    /// </summary>
    private string? SecondCheckRefusal(CalcInstance instance, CalcTemplate template, string subject)
    {
        if (!template.RequiresSecondCheck)
            return null;

        return CountersignatureRefusal(instance.SecondCheck, instance.CheckedBy, instance.CheckerPrincipal, instance.Authors, subject);
    }

    private string? CountersignatureRefusal(CalcCountersignature? second, string? firstName, string? firstPrincipal, IEnumerable<string> authors, string subject)
    {
        if (second is null)
            return $"{subject} needs a second, independent check (a countersignature by a different checker) before it can be issued.";

        if (!second.Accepted)
            return $"{subject} was rejected by its second checker ({second.Name}): {second.Statement}".TrimEnd() + " Revise it to correct and check it again.";

        if (Same(second.Name, firstName) || Same(second.Principal, firstPrincipal) || Same(second.Name, firstPrincipal) || Same(second.Principal, firstName))
            return $"{subject} needs two different checkers: the second check was signed by the person who made the first.";

        foreach (var author in authors)
        {
            if (Same(second.Name, author) || Same(second.Principal, author))
                return $"{subject} needs an independent second check: '{second.Name}' created or edited it ({Describe(author)}).";
        }

        return null;
    }

    /// <summary>The first thing a template's extra governance refuses at an accepting Check: the attestation (the second check comes after the first).</summary>
    private string? CheckGovernanceRefusal(CalcInstance instance, CalcTemplate template) =>
        AttestationRefusal(instance, template, $"Calculation '{instance.Name}'");

    /// <summary>Everything a template's extra governance demands before Issue: the attestation, then the second check.</summary>
    private string? IssueGovernanceRefusal(CalcInstance instance, CalcTemplate template) =>
        AttestationRefusal(instance, template, $"Calculation '{instance.Name}'") ?? SecondCheckRefusal(instance, template, $"Calculation '{instance.Name}'");

    /// <summary>The checker line a sheet's header shows: the first checker, then the second when there is one that accepted.</summary>
    private static string? CheckerLabel(string? first, CalcCountersignature? second) =>
        second is { Accepted: true } ? $"{(string.IsNullOrWhiteSpace(first) ? "—" : first)}; second check: {second.Name}" : first;

    /// <summary>The note stamped into an issued snapshot when the calculator is gated: the feature and where it was enabled.</summary>
    private string? FeatureNoteFor(CalcTemplate template) =>
        template.FeatureFlag is { Length: > 0 } flag ? $"{flag} feature enabled ({_catalogue.Features.SourceOf(flag) ?? "source not recorded"})" : null;

    /// <summary>
    /// Records the competent person's attestation of a draft calculation whose template requires one (ADR-0173), made by the signed-in
    /// principal: the attester must be a known person who is none of the calculation's authors, and the name, qualification and statement they give
    /// are written into the sheet's attestation inputs with the attester as their source. A later change to any input voids the attestation. The
    /// checkers must in turn be someone other than the attester.
    /// </summary>
    public async Task<CalcActionResult> AttestAsync(Guid instanceId, string name, string qualification, string statement, CancellationToken cancellationToken = default)
    {
        var cleanName = CalcSheetTextRenderer.Clean(name ?? string.Empty).Trim();
        var cleanQualification = CalcSheetTextRenderer.Clean(qualification ?? string.Empty).Trim();
        var cleanStatement = CalcSheetTextRenderer.Clean(statement ?? string.Empty).Trim();

        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return CalcActionResult.Refused($"Template '{instance.TemplateReference}' is no longer registered.", instance);

        if (!entry.Template.RequiresAttestation)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' does not take an attestation: its template does not require one.", instance);

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; it is attested while it is a draft, before it is checked.", instance);

        if (instance.PackId is { } packId && await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false) is { Status: not CalcInstanceStatus.Draft } pack)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' belongs to pack '{pack.Title}', which is {pack.Status}; its inputs are frozen.", instance);

        if (cleanName.Length < MinAttestationFieldLength)
            return CalcActionResult.Refused($"The competent person's name is needed (at least {MinAttestationFieldLength} characters).", instance);

        if (cleanQualification.Length < MinAttestationFieldLength)
            return CalcActionResult.Refused($"The competent person's qualification is needed (at least {MinAttestationFieldLength} characters).", instance);

        if (cleanStatement.Length < CalcAttestation.MinStatementLength)
            return CalcActionResult.Refused($"The attestation statement is needed: at least {CalcAttestation.MinStatementLength} characters saying what the competent person has reviewed.", instance);

        var principal = CurrentPrincipal();

        if (!IsKnownPerson(principal))
            return CalcActionResult.Refused($"Calculation '{instance.Name}' needs an attestation by a signed-in competent person: nobody is signed in.", instance);

        foreach (var author in instance.Authors)
        {
            if (Same(principal, author) || Same(cleanName, author))
                return CalcActionResult.Refused($"Calculation '{instance.Name}' needs an independent competent-person attestation: sign in as a competent person other than its author or an editor ({Describe(author)}).", instance);
        }

        var now = _time.GetUtcNow();
        var basis = CalcProvenance.Assumed(CalcSheetTextRenderer.Clean($"Attested by {Describe(principal)} on {now:yyyy-MM-dd}"));
        var inputs = instance.Inputs.Where(i => !CalcAttestation.Symbols.Contains(i.Symbol, StringComparer.Ordinal)).ToList();
        inputs.Add(CalcInputValue.Choice(CalcAttestation.NameSymbol, cleanName, basis));
        inputs.Add(CalcInputValue.Choice(CalcAttestation.QualificationSymbol, cleanQualification, basis));
        inputs.Add(CalcInputValue.Choice(CalcAttestation.StatementSymbol, cleanStatement, basis));

        var record = new CalcAttestationRecord(principal, cleanName, cleanQualification, cleanStatement, now, HashInputs(inputs));
        var updated = instance with { Inputs = inputs, Attestation = record, UpdatedAt = now };

        try
        {
            await _store.SaveInstanceAsync(updated, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException or CalcDocumentTooLargeException)
        {
            return CalcActionResult.Refused(ex.Message, await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) ?? instance);
        }

        return CalcActionResult.Ok(updated);
    }

    /// <summary>
    /// Records the second, independent check of a checked calculation whose template requires one (ADR-0173). The signed-in person must
    /// be known, must not have created the calculation or set or changed any of its inputs, must not be its attester, and must not be the person
    /// who made the first check; the typed name is held to the same rules. A rejection is kept on the instance and refuses Issue, and a checked
    /// calculation with a rejected second check can be revised.
    /// </summary>
    public async Task<CalcActionResult> CountersignAsync(Guid instanceId, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome, CancellationToken cancellationToken = default)
    {
        if ((MissingText(checkerName, "A second checker name") ?? MissingText(checkerOrganisation, "The second checker's organisation") ?? SignatureStatementProblem(statement)) is { } missing)
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

        if (IndependenceRefusal(instance, $"'{instance.Name}'", "countersign it", null, out _) is { } notIndependent)
            return CalcActionResult.Refused(notIndependent, instance);

        var principal = CurrentPrincipal();

        if (Same(checkerName, instance.CheckedBy) || Same(principal, instance.CheckerPrincipal) || Same(principal, instance.CheckedBy) || Same(checkerName, instance.CheckerPrincipal))
            return CalcActionResult.Refused($"'{instance.Name}' needs two different checkers: sign in as someone other than the first checker ({instance.CheckedBy}) to countersign it.", instance);

        foreach (var author in instance.Authors)
        {
            if (Same(checkerName, author))
                return CalcActionResult.Refused($"'{instance.Name}' needs an independent second check: '{checkerName}' created or edited it ({Describe(author)}).", instance);
        }

        if (AttesterIsSigningRefusal(instance, entry.Template, checkerName, $"'{instance.Name}'") is { } attester)
            return CalcActionResult.Refused(attester, instance);

        var now = _time.GetUtcNow();
        var signature = new CalcCountersignature(CalcSheetTextRenderer.Clean(checkerName.Trim()), CalcSheetTextRenderer.Clean(checkerOrganisation.Trim()), CalcSheetTextRenderer.Clean(statement).Trim(), principal, now, outcome != CheckOutcome.Rejected);
        var updated = instance with { SecondCheck = signature, UpdatedAt = now };

        try
        {
            await _store.SaveInstanceAsync(updated, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException or CalcDocumentTooLargeException)
        {
            return CalcActionResult.Refused(ex.Message, await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) ?? instance);
        }

        return signature.Accepted
            ? CalcActionResult.Ok(updated)
            : CalcActionResult.Refused($"The second checker rejected the calculation; it stays checked but cannot be issued. {signature.Statement}".TrimEnd() + " Revise it to correct and check it again.", updated);
    }

    /// <summary>A countersignature or rejection says what was verified or why it is rejected: at least 20 characters, as for the attestation.</summary>
    private static string? SignatureStatementProblem(string? statement) =>
        (statement ?? string.Empty).Trim().Length < MinSignatureStatementLength ? $"A statement of at least {MinSignatureStatementLength} characters is needed: what the second checker verified, or why they reject it." : null;

    /// <summary>
    /// The pack form of <see cref="CountersignAsync"/>: a checked pack with a member whose template requires a second check takes the
    /// second signature on the pack, from a person who is none of the members' authors or attesters and not the pack's first checker.
    /// </summary>
    public async Task<CalcActionResult> CountersignPackAsync(Guid packId, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome, CancellationToken cancellationToken = default)
    {
        if ((MissingText(checkerName, "A second checker name") ?? MissingText(checkerOrganisation, "The second checker's organisation") ?? SignatureStatementProblem(statement)) is { } missing)
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

            if (IndependenceRefusal(member, $"Pack member '{member.Name}'", "countersign the pack", null, out _) is { } notIndependent)
                return CalcActionResult.Refused(notIndependent);

            if (_catalogue.Find(member.TemplateReference)?.Template is { } template && AttesterIsSigningRefusal(member, template, checkerName, $"Pack member '{member.Name}'") is { } attester)
                return CalcActionResult.Refused(attester);
        }

        var principal = CurrentPrincipal();

        if (!IsKnownPerson(principal))
            return CalcActionResult.Refused($"Pack '{pack.Title}' needs an independent second check: nobody is signed in, so there is no one to countersign it.");

        if (Same(checkerName, pack.CheckerName) || Same(principal, pack.CheckerPrincipal) || Same(principal, pack.CheckerName) || Same(checkerName, pack.CheckerPrincipal))
            return CalcActionResult.Refused($"Pack '{pack.Title}' needs two different checkers: sign in as someone other than the first checker ({pack.CheckerName}) to countersign it.");

        foreach (var author in members.SelectMany(m => m.Authors).Distinct(StringComparer.Ordinal))
        {
            if (Same(checkerName, author) || Same(principal, author))
                return CalcActionResult.Refused($"Pack '{pack.Title}' needs an independent second check: '{checkerName}' created or edited one of its calculations ({Describe(author)}).");
        }

        var now = _time.GetUtcNow();
        var signature = new CalcCountersignature(CalcSheetTextRenderer.Clean(checkerName.Trim()), CalcSheetTextRenderer.Clean(checkerOrganisation.Trim()), CalcSheetTextRenderer.Clean(statement).Trim(), principal, now, outcome != CheckOutcome.Rejected);

        try
        {
            await _store.SavePackAsync(pack with { SecondCheck = signature, UpdatedAt = now }, pack.UpdatedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException or CalcDocumentTooLargeException)
        {
            return CalcActionResult.Refused(ex.Message);
        }

        return signature.Accepted
            ? new CalcActionResult(true, null, null)
            : CalcActionResult.Refused($"The second checker rejected the pack; it stays checked but cannot be issued. {signature.Statement}".TrimEnd() + " Reopen it to correct and check it again.");
    }
}
