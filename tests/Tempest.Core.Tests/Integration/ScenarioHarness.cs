using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Population;

namespace Tempest.Core.Tests.Integration;

/// <summary>
/// The seeded corpus, plus the one thing a test may do that the product may
/// not: pretend a person has reviewed a record.
/// </summary>
/// <remarks>
/// <para>
/// <b>FICTIONAL REVIEWER. TEST SCOPE ONLY.</b> The shipped seed corpus is
/// Draft and stays Draft; nothing outside <c>tests/</c> calls anything on
/// this class. It exists because the integration phase has to prove two
/// opposite things, and only one of them can be proved with real data:
/// </para>
/// <list type="number">
/// <item>that the reasoning services refuse Draft reference data — proved
/// against the corpus exactly as it ships; and</item>
/// <item>that they consume Released reference data correctly — which
/// cannot be proved at all until something is Released.</item>
/// </list>
/// <para>
/// The alternative to a clearly-labelled fictional reviewer in a test was
/// to leave the entire consumption path unexercised until a human review
/// happens, which would mean shipping an integration phase that never
/// integrated anything. The reviewer's identity says what it is in every
/// field a reader could look at, and
/// <c>RefusalTests.NothingOutsideATestCanForgeAReview</c> asserts that the
/// shipped corpus is untouched by it.
/// </para>
/// </remarks>
internal sealed class ScenarioHarness : SeedHarness
{
    /// <summary>
    /// The principal id written into any record this harness releases.
    /// </summary>
    /// <remarks>
    /// Deliberately not a plausible human name. Anything that reads this
    /// value out of a record and shows it to a person should make somebody
    /// ask what it is doing there.
    /// </remarks>
    public const string FictionalReviewerPrincipalId = "TEST-FICTIONAL-REVIEWER-NOT-A-PERSON";

    /// <summary>The verification date this harness writes. A fixed date, so a test never depends on the day it runs.</summary>
    public static readonly DateOnly FictionalReviewDate = new(2026, 9, 7);

    /// <summary>
    /// Walks one record from <see cref="ReferenceValidationState.Draft"/> to
    /// <see cref="ReferenceValidationState.Released"/> as a reviewer would,
    /// writing a provenance that says openly that the reviewer is fictional.
    /// </summary>
    /// <typeparam name="TDefinition">The library's definition type.</typeparam>
    /// <param name="catalog">The library holding the record.</param>
    /// <param name="recordId">The record to release.</param>
    /// <returns>The released record.</returns>
    public static async Task<IReferenceRecord<TDefinition>> ReleaseForTestingAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        string recordId)
        where TDefinition : class
    {
        var record = await catalog.FindAsync(recordId)
            ?? throw new InvalidOperationException($"No record '{recordId}' in {catalog.LibraryName}.");

        // The provenance revision is the review. Without it the lifecycle
        // refuses the release outright, which is the behaviour the refusal
        // tests rely on.
        await catalog.ReviseAsync(
            recordId,
            record.Definition,
            record.Provenance with
            {
                VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
                ReviewerPrincipalId = FictionalReviewerPrincipalId,
                VerificationDate = FictionalReviewDate,
                Notes = (record.Provenance.Notes is null ? string.Empty : record.Provenance.Notes + " ")
                    + "TEST-ONLY REVIEW: this record was released inside an automated test by a fictional "
                    + "reviewer. No person checked it against its source. A record carrying this note must "
                    + "never be treated as verified reference data.",
            },
            "TEST-ONLY: fictional review recorded so the released consumption path can be exercised.");

        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Checked, "TEST-ONLY: fictional check.");
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Validated, "TEST-ONLY: fictional validation.");
        return await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Released, "TEST-ONLY: fictional release.");
    }

    /// <summary>
    /// Releases the records the bracket scenario needs in order to reach
    /// P02's reasoning services at all.
    /// </summary>
    /// <returns>The record identities released.</returns>
    public async Task<IReadOnlyList<string>> ReleaseScenarioDataForTestingAsync()
    {
        var released = new List<string>();

        foreach (var materialId in ScenarioRecords.CandidateMaterials)
        {
            await ReleaseForTestingAsync(Materials, materialId);
            released.Add(materialId);
        }

        foreach (var processId in ScenarioRecords.CandidateProcesses)
        {
            await ReleaseForTestingAsync(Processes, processId);
            released.Add(processId);
        }

        foreach (var ruleId in ScenarioRecords.ScenarioRules)
        {
            await ReleaseForTestingAsync(Rules, ruleId);
            released.Add(ruleId);
        }

        return released;
    }
}
