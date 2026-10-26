using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using CoreEvidence = Tempest.Core.Evidence.Evidence;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>What an issued revision looked like the moment it was issued: the values every later look must find unchanged.</summary>
internal sealed record IssuedFingerprint(
    Guid Id,
    Guid? ProjectId,
    string Revision,
    Guid EvidenceId,
    string ContentHash,
    string OutputsHash,
    string SheetTextHash,
    string AttachmentHash,
    long AttachmentBytes,
    Guid AttachmentId);

internal static partial class SoakScenarios
{
    // ------------------------------------------------------------------
    // 3. Cycles: revise, check, issue, supersede, many times; an issued revision never changes
    // ------------------------------------------------------------------

    private const string CycleTemplate = "mech.iso286-fit";

    /// <summary>
    /// One calculation is issued, then revised, changed, checked by someone else and issued again <see cref="SoakScale.Cycles"/> times, so
    /// the previous revision is superseded every time. After every step the fingerprint of every issued revision so far is compared
    /// with what it was at issue (the stored record with its status and time set aside, the sheet text, the PDF's hash and bytes in the
    /// content store); every attempt to edit an issued or superseded revision, through the service, the store and the generic Evidence
    /// acts, must be refused and must leave the stored record byte for byte as it was.
    /// </summary>
    public static async Task CyclesAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-3", "Soak: revision cycles");
        var example = SoakExamples.For(CycleTemplate)[0].Inputs;
        var fingerprints = new List<IssuedFingerprint>();
        var revise = new Stopwatch();
        var check = new Stopwatch();
        var issue = new Stopwatch();
        var verify = new Stopwatch();
        var refusals = 0;

        var databaseBefore = DatabaseBytes(host);
        host.SignInAs(SoakHost.Author);
        var current = await service.CreateAsync(CycleTemplate, project.Id, "Soak revision subject", example, cancellationToken);
        var firstRevision = current.Revision;
        var longestRevision = firstRevision.Length;

        for (var cycle = 0; cycle <= scale.Cycles; cycle++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cycle > 0)
            {
                host.SignInAs(SoakHost.Author);
                revise.Start();
                var revised = await service.ReviseAsync(current.Id, cancellationToken);
                revise.Stop();

                if (!revised.Succeeded || revised.Instance is null)
                {
                    outcome.Expect(false, () => $"Cycle {cycle}: revising {current.Revision} was refused: {revised.Reason}");
                    break;
                }

                var previous = fingerprints[^1];
                current = revised.Instance;
                longestRevision = Math.Max(longestRevision, current.Revision.Length);
                outcome.Expect(current.Status == CalcInstanceStatus.Draft && current.PredecessorId == previous.Id, () => $"Cycle {cycle}: the new revision {current.Revision} is not a draft following {previous.Revision}.");

                // The revision just superseded is superseded, and is otherwise exactly what was issued.
                await VerifyAsync(host, previous, CalcInstanceStatus.Superseded, outcome, $"cycle {cycle}, just superseded");

                // The new draft takes a new value, so no two revisions are alike.
                var edit = await service.UpdateInputsAsync(current.Id, [.. current.Inputs.Select(i => i.Symbol == "D" ? i with { Number = 25 + cycle * 0.01 } : i)], cancellationToken);
                outcome.Expect(edit.Succeeded, () => $"Cycle {cycle}: editing the new draft was refused: {edit.Reason}");
                current = edit.Instance ?? current;
            }

            host.SignInAs(SoakHost.Checker);
            check.Start();
            var checkedResult = await service.CheckAsync(current.Id, "Soak Checker", "Soak Checking Ltd", $"Checked revision {current.Revision} by the soak harness.", CheckOutcome.Accepted, cancellationToken);
            check.Stop();

            if (!checkedResult.Succeeded)
            {
                outcome.Expect(false, () => $"Cycle {cycle}: checking {current.Revision} was refused: {checkedResult.Reason}");
                break;
            }

            host.SignInAs(SoakHost.Author);
            issue.Start();
            var issued = await service.IssueAsync(current.Id, $"SOAK-{cycle:000}", "Soak Client Ltd", cancellationToken);
            issue.Stop();

            if (!issued.Succeeded || issued.Instance is null)
            {
                outcome.Expect(false, () => $"Cycle {cycle}: issuing {current.Revision} was refused: {issued.Reason}");
                break;
            }

            current = issued.Instance;
            var fingerprint = await CaptureAsync(host, current, outcome, $"cycle {cycle}");

            if (fingerprint is null)
                break;

            fingerprints.Add(fingerprint);

            // Edit attempts on the revision just issued.
            refusals += await TryToEditAsync(host, current, outcome, $"cycle {cycle}, issued");

            // The whole history so far is still what it was (every ten cycles in full, to keep the work linear).
            if (cycle % 10 == 0 || cycle == scale.Cycles)
            {
                verify.Start();

                for (var i = 0; i < fingerprints.Count - 1; i++)
                    await VerifyAsync(host, fingerprints[i], CalcInstanceStatus.Superseded, outcome, $"cycle {cycle}, history");

                verify.Stop();

                // And the superseded ones refuse edits as firmly as the issued one: the oldest and the one just superseded.
                foreach (var old in fingerprints.Count > 1 ? new[] { fingerprints[0], fingerprints[^2] }.DistinctBy(f => f.Id) : [])
                {
                    if (await service.Store.FindInstanceAsync(old.Id) is { } superseded)
                        refusals += await TryToEditAsync(host, superseded, outcome, $"cycle {cycle}, superseded");
                }
            }

            if ((cycle + 1) % 10 == 0)
                SoakScale.Progress($"cycles: {cycle + 1} of {scale.Cycles + 1}");
        }

        // The last word: every revision ever issued, once more, and the chain of revisions itself.
        for (var i = 0; i < fingerprints.Count; i++)
            await VerifyAsync(host, fingerprints[i], i == fingerprints.Count - 1 ? CalcInstanceStatus.Issued : CalcInstanceStatus.Superseded, outcome, "at the end");

        var listed = await service.Store.ListInstancesAsync(project.Id, cancellationToken);
        outcome.Expect(listed.Count == fingerprints.Count, () => $"The project holds {listed.Count} revisions; {fingerprints.Count} were issued.");
        outcome.Expect(listed.Count(i => i.Status == CalcInstanceStatus.Issued) == 1, () => "More than one revision is left Issued; each issue supersedes the one before.");
        outcome.Expect(listed.Count(i => i.Status == CalcInstanceStatus.Superseded) == Math.Max(0, fingerprints.Count - 1), () => $"{listed.Count(i => i.Status == CalcInstanceStatus.Superseded)} revisions are Superseded; {Math.Max(0, fingerprints.Count - 1)} were.");
        outcome.Expect(listed.Select(i => i.Revision).Distinct(StringComparer.Ordinal).Count() == listed.Count, () => "Two revisions carry the same revision label.");
        outcome.Expect(listed.Select(i => i.EvidenceId).Distinct().Count() == listed.Count, () => "Two revisions share one Evidence record.");
        var byId = listed.ToDictionary(i => i.Id);
        var walked = 0;

        for (var at = fingerprints.Count == 0 ? null : byId.GetValueOrDefault(fingerprints[^1].Id); at is not null; at = at.PredecessorId is { } p ? byId.GetValueOrDefault(p) : null)
            walked++;

        outcome.Expect(walked == fingerprints.Count, () => $"Following predecessors from the last revision reaches {walked} revisions; {fingerprints.Count} were issued.");

        if (fingerprints.Count > 1)
        {
            var (comparison, reason) = await service.CompareWithPredecessorAsync(fingerprints[^1].Id, cancellationToken);
            outcome.Expect(comparison is not null, () => $"The last revision cannot be compared with the one before it: {reason}");
        }

        outcome.Fact("Revisions issued", fingerprints.Count);
        outcome.Fact("Database growth per issued revision (KB)", fingerprints.Count == 0 ? 0 : (DatabaseBytes(host) - databaseBefore) / 1024.0 / fingerprints.Count, "0.0");
        outcome.Fact("Revise → supersede (mean ms)", revise.Elapsed.TotalMilliseconds / Math.Max(1, fingerprints.Count - 1), "0.0");
        outcome.Fact("Check (mean ms)", check.Elapsed.TotalMilliseconds / Math.Max(1, fingerprints.Count), "0.0");
        outcome.Fact("Issue incl. PDF sheet (mean ms)", issue.Elapsed.TotalMilliseconds / Math.Max(1, fingerprints.Count), "0.0");
        outcome.Fact("Edit attempts refused", refusals);
        outcome.Fact("Immutability checks of issued revisions", outcome.Checks);
        outcome.Fact("Longest revision label (characters)", longestRevision);
        outcome.Fact("PDF sheet (mean KB)", fingerprints.Count == 0 ? 0 : fingerprints.Average(f => f.AttachmentBytes) / 1024.0, "0.0");
    }

    /// <summary>Records what an issued revision looks like: the stored record without its status and time, the sheet text, and the Evidence attachment.</summary>
    private static async Task<IssuedFingerprint?> CaptureAsync(SoakHost host, CalcInstance issued, SoakOutcome outcome, string label)
    {
        outcome.Expect(issued is { Status: CalcInstanceStatus.Issued, Issue: not null, EvidenceId: not null }, () => $"{label}: the issued revision came back without its issue snapshot.");

        if (issued.Issue is not { SheetAttachmentId: { } attachmentText } snapshot || issued.EvidenceId is not { } evidenceId || !Guid.TryParseExact(attachmentText, "N", out var attachmentId))
            return null;

        var stored = await host.Service.Store.FindInstanceAsync(issued.Id);
        outcome.Expect(stored is { Status: CalcInstanceStatus.Issued }, () => $"{label}: the store does not hold the revision as Issued.");

        if (stored is null)
            return null;

        var attachment = await ReadAttachmentAsync(host, evidenceId, attachmentId);
        outcome.Expect(attachment is { Bytes.Length: > 1000 } && Encoding.ASCII.GetString(attachment.Bytes.AsSpan(0, 4)) == "%PDF", () => $"{label}: the issued sheet is not a PDF in the content store.");

        if (attachment is null)
            return null;

        return new IssuedFingerprint(
            issued.Id, issued.ProjectId, issued.Revision, evidenceId, Canonical(stored), stored.OutputsHash ?? string.Empty, Hash(snapshot.SheetText), attachment.Hash, attachment.Bytes.Length, attachmentId);
    }

    /// <summary>Compares an issued revision, as it is now, with its fingerprint.</summary>
    private static async Task VerifyAsync(SoakHost host, IssuedFingerprint expected, CalcInstanceStatus status, SoakOutcome outcome, string label)
    {
        var now = await host.Service.Store.FindInstanceAsync(expected.Id);

        if (now is null)
        {
            outcome.Expect(false, () => $"{label}: revision {expected.Revision} has gone from the store.");
            return;
        }

        outcome.Expect(now.Status == status, () => $"{label}: revision {expected.Revision} is {now.Status}; expected {status}.");
        outcome.Expect(Canonical(now) == expected.ContentHash, () => $"{label}: the stored record of issued revision {expected.Revision} has changed since it was issued (IMMUTABILITY BREACH).");
        outcome.Expect(now.OutputsHash == expected.OutputsHash, () => $"{label}: the outputs of issued revision {expected.Revision} have changed.");
        outcome.Expect(now.Issue is not null && Hash(now.Issue.SheetText) == expected.SheetTextHash, () => $"{label}: the sheet text of issued revision {expected.Revision} has changed.");

        var attachment = await ReadAttachmentAsync(host, expected.EvidenceId, expected.AttachmentId);
        outcome.Expect(attachment is not null && attachment.Hash == expected.AttachmentHash && attachment.Bytes.Length == expected.AttachmentBytes, () => $"{label}: the issued sheet (PDF) of revision {expected.Revision} has changed or gone (IMMUTABILITY BREACH).");
        var evidence = await host.Domain.Repository.FindAsync(expected.EvidenceId) as CoreEvidence;
        outcome.Expect(evidence is { Status: EvidenceStatus.Issued }, () => $"{label}: the Evidence of revision {expected.Revision} is {evidence?.Status.ToString() ?? "missing"}, not Issued.");
    }

    private sealed record StoredAttachment(byte[] Bytes, string Hash);

    /// <summary>The bytes of an Evidence attachment as the content store verifies and returns them, with their SHA-256; <see langword="null"/> when missing or corrupt.</summary>
    private static async Task<StoredAttachment?> ReadAttachmentAsync(SoakHost host, Guid evidenceId, Guid attachmentId)
    {
        if (await host.Domain.Repository.FindAsync(evidenceId) is not IHasAttachments owner)
            return null;

        var meta = (await owner.GetAttachmentsAsync()).FirstOrDefault(a => a.Id == attachmentId);

        if (meta is null)
            return null;

        var content = await host.Domain.AttachmentContentStore.ReadAsync(attachmentId, meta.ContentHash, meta.SizeInBytes);

        return content.IsAvailable ? new StoredAttachment(content.Bytes, Convert.ToHexStringLower(SHA256.HashData(content.Bytes))) : null;
    }

    /// <summary>The stored record with the two fields supersession may change set aside, as one hash: what "byte-identical" means for an issued revision.</summary>
    private static string Canonical(CalcInstance instance) =>
        Hash(CalcInstanceStore.Serialise(instance with { Status = CalcInstanceStatus.Issued, UpdatedAt = default }));

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>
    /// Tries every way of changing a finished revision that the application, the store or the generic Evidence screens offer. Each must be
    /// refused, and the stored record must be byte for byte what it was before the attempts. Returns how many were refused.
    /// </summary>
    private static async Task<int> TryToEditAsync(SoakHost host, CalcInstance target, SoakOutcome outcome, string label)
    {
        var service = host.Service;
        var before = await host.RawInstanceAsync(target);
        var refused = 0;

        void Refused(bool wasRefused, string what)
        {
            refused += wasRefused ? 1 : 0;
            outcome.Expect(wasRefused, () => $"{label}: {what} on {target.Name} rev {target.Revision} was NOT refused (IMMUTABILITY BREACH).");
        }

        var changed = target.Inputs.Select(i => i.Symbol == "D" ? i with { Number = (i.Number ?? 25) + 5 } : i).ToList();
        Refused(!(await service.UpdateInputsAsync(target.Id, changed)).Succeeded, "changing its inputs");
        Refused(!(await service.UpdateInputsAsync(target.Id, changed, target.UpdatedAt)).Succeeded, "changing its inputs from a fresh read");
        Refused(!(await service.CheckAsync(target.Id, "Soak Checker", "Soak Checking Ltd", "Checked again.", CheckOutcome.Accepted)).Succeeded, "checking it again");
        Refused(!(await service.IssueAsync(target.Id, "SOAK-AGAIN", "Soak Client Ltd")).Succeeded, "issuing it again");
        Refused(!(await service.BindAsync(target.Id, "D", target.Id, ChainOutput)).Succeeded, "linking one of its inputs");
        Refused(!(await service.UpdateLinksAsync(target.Id)).Succeeded, "updating its links");
        Refused(!(await service.ApplyCadImportAsync(target.Id, EmptyCadRequest())).Action.Succeeded, "applying a CAD import to it");

        // Below every service: the store itself refuses to overwrite it.
        var tampered = target with { Inputs = changed, OutputsHash = "tampered" };
        var storeRefused = false;

        try
        {
            await service.Store.SaveInstanceAsync(tampered);
        }
        catch (CalcInstanceImmutableException)
        {
            storeRefused = true;
        }

        Refused(storeRefused, "overwriting it in the store");

        // The generic Evidence acts do not reach a calculation's Evidence at all.
        if (target.EvidenceId is { } evidenceId)
        {
            Refused(!(await host.Evidence.RecordCheckAsync(evidenceId, "Soak Checker", "Soak Checking Ltd", "Checked through the Evidence screen.", CheckOutcome.Accepted)).Succeeded, "checking its Evidence through the Evidence service");
            Refused(!(await host.Evidence.ReviseAsync(evidenceId)).Succeeded, "revising its Evidence through the Evidence service");
            Refused(!(await host.Evidence.IssueAsync(evidenceId, "SOAK-GENERIC", target.Revision, "Soak Client Ltd")).Succeeded, "issuing its Evidence through the Evidence service");
        }

        var after = await host.RawInstanceAsync(target);
        outcome.Expect(string.Equals(before, after, StringComparison.Ordinal), () => $"{label}: the stored record of {target.Name} rev {target.Revision} changed during the edit attempts (IMMUTABILITY BREACH).");
        return refused;
    }

    private static CadImportRequest EmptyCadRequest() =>
        new(new Tempest.Calc.Import.ImportedFile("none.exp", Encoding.UTF8.GetBytes("[mm]D=25\n"), Hash("none")), CadSourceFormat.NxExpressions,
            new CadMappingProfile("soak-edit", 1, "Soak edit attempt", [new CadMappingEntry("D", "D")], CadSystem.Nx, CycleTemplate));
}
