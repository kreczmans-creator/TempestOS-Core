using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Fasteners;

/// <summary>The thread system a thread designation belongs to.</summary>
public enum ThreadSystem
{
    /// <summary>Not recorded.</summary>
    Unspecified,

    /// <summary>A metric thread with the coarse pitch for its diameter.</summary>
    MetricCoarse,

    /// <summary>A metric thread with a pitch other than the coarse one.</summary>
    MetricFine,

    /// <summary>An inch-series thread with the coarse pitch for its diameter.</summary>
    UnifiedCoarse,

    /// <summary>An inch-series thread with the fine pitch for its diameter.</summary>
    UnifiedFine,

    /// <summary>A thread intended to make a pipe or fluid connection rather than a mechanical joint.</summary>
    Pipe,

    /// <summary>A trapezoidal or acme power-transmission thread.</summary>
    Trapezoidal,

    /// <summary>A thread system this taxonomy does not classify. <see cref="FastenerDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}

/// <summary>Which way a thread turns.</summary>
public enum ThreadHandedness
{
    /// <summary>Not recorded. Never read as right-handed by default: a left-hand thread fitted as a right-hand one fails.</summary>
    Unspecified,

    /// <summary>Right-handed.</summary>
    RightHand,

    /// <summary>Left-handed.</summary>
    LeftHand
}

/// <summary>
/// A fastener's own thread, as a source specified it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Designation"/> is the source's own thread designation kept
/// verbatim, and is the only required field: a source that quotes a
/// designation without breaking out the diameter and pitch has still told
/// us something exact, and inventing the missing numbers from the
/// designation would be deriving data and presenting it as source data.
/// </para>
/// <para>
/// <b>Pitch, not threads per inch.</b> Pitch is the physical quantity, and
/// it is recorded as a <see cref="Length"/> through the platform's own
/// Units &amp; Quantities framework. A threads-per-inch count is a
/// designation convention rather than a dimensioned quantity of its own,
/// it is fully determined by the pitch, and giving it a field would invite
/// a second, silently inconsistent answer to one question. Where a source
/// quotes only a thread count, <see cref="Designation"/> preserves it
/// exactly as written.
/// </para>
/// </remarks>
/// <param name="Designation">The thread designation as the source writes it. Required.</param>
/// <param name="System">The thread system the designation belongs to.</param>
/// <param name="NominalDiameter">The nominal (major) thread diameter. <see langword="null"/> if the source stated only a designation.</param>
/// <param name="Pitch">The thread pitch — the axial distance between adjacent thread crests. <see langword="null"/> if not recorded.</param>
/// <param name="Handedness">Which way the thread turns.</param>
/// <param name="ToleranceClass">The thread tolerance or fit class as the source designates it. <see langword="null"/> if not recorded.</param>
/// <param name="ThreadLength">The length of thread on the fastener, where it is threaded over part of its length only. <see langword="null"/> if fully threaded or not recorded.</param>
public sealed record ThreadSpecification(
    string Designation,
    ThreadSystem System = ThreadSystem.Unspecified,
    ReferenceValue<Length>? NominalDiameter = null,
    ReferenceValue<Length>? Pitch = null,
    ThreadHandedness Handedness = ThreadHandedness.Unspecified,
    string? ToleranceClass = null,
    ReferenceValue<Length>? ThreadLength = null)
{
    /// <summary>The thread designation as the source writes it.</summary>
    public string Designation { get; } = string.IsNullOrWhiteSpace(Designation)
        ? throw new ArgumentException("A thread specification must carry a designation.", nameof(Designation))
        : Designation.Trim();

    /// <summary>Whether the thread system is a metric one.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsMetric => System is ThreadSystem.MetricCoarse or ThreadSystem.MetricFine;
}
