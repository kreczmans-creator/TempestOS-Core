namespace Tempest.Calc.Library.ManualTesting;

/// <summary>
/// The latest manual-test result for one worksheet template (Addendum C, C-5): the worst of the latest result of each of the template's
/// worked examples (Fail over Blocked over Pass), with the date of the most recent of them. A step the tester skipped is not a result and is
/// left out; a template with no result recorded is absent from <see cref="IManualTestStatusSource.LatestByTemplateAsync"/>.
/// </summary>
/// <param name="TemplateId">The worksheet template id (for example <c>mech.bearing-life</c>).</param>
/// <param name="Outcome">Pass, Fail or Blocked (never Skipped).</param>
/// <param name="LastRecordedUtc">When the most recent of the counted results was recorded, UTC.</param>
/// <param name="BuildCommit">The commit of the build that most recent result was recorded on.</param>
/// <param name="Tester">The tester of that result.</param>
/// <param name="RunId">The run holding that result.</param>
/// <param name="StepsRecorded">How many of the template's worked examples have a result.</param>
/// <param name="StepsInScript">How many worked examples the current script holds for the template.</param>
/// <param name="Build">The build that most recent result was recorded on, as the title bar showed it (for example <c>0.26.0 (abc1234)</c>); empty when the run recorded none (SEC-C-06).</param>
public sealed record ManualTestStatus(
    string TemplateId,
    ManualTestOutcome Outcome,
    DateTimeOffset LastRecordedUtc,
    string BuildCommit,
    string Tester,
    Guid RunId,
    int StepsRecorded,
    int StepsInScript,
    string Build = "");

/// <summary>
/// A read-only view of the manual test results for the verification dashboard (Addendum C, C-4): template id to its latest manual result.
/// It lives in <c>Tempest.Calc.Library</c> (a non-UI project the Desktop and the tests already reference), is implemented by
/// <see cref="ManualTestRunService"/>, and has no way to write: a dashboard that holds only this interface cannot change a run, a calculation or
/// Evidence.
/// </summary>
public interface IManualTestStatusSource
{
    /// <summary>The latest manual result of every template that has one, keyed by template id (ordinal).</summary>
    Task<IReadOnlyDictionary<string, ManualTestStatus>> LatestByTemplateAsync(CancellationToken cancellationToken = default);
}
