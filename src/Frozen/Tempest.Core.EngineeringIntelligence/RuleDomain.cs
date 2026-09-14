namespace Tempest.Core.EngineeringIntelligence;

/// <summary>The area of mechanical engineering a rule belongs to.</summary>
/// <remarks>
/// A retrieval and organisation axis, not a semantic one: nothing about
/// how a rule evaluates depends on its domain. It exists so a rule library
/// can be browsed and filtered by the discipline an engineer is working
/// in, which is how an engineer actually looks for a rule.
/// </remarks>
public enum RuleDomain
{
    /// <summary>Not recorded. The honest default.</summary>
    Unspecified,

    /// <summary>Material choice, condition and property limits.</summary>
    Materials,

    /// <summary>Threaded and non-threaded fasteners.</summary>
    Fasteners,

    /// <summary>Rolling bearings.</summary>
    Bearings,

    /// <summary>Springs.</summary>
    Springs,

    /// <summary>Gears and geared drives.</summary>
    Gears,

    /// <summary>Shafts and shaft features.</summary>
    Shafts,

    /// <summary>Belts, chains, couplings and other power-transmission elements.</summary>
    PowerTransmission,

    /// <summary>Joints, whether bolted, welded, bonded or interference.</summary>
    Joints,

    /// <summary>Fits, clearances and dimensional tolerancing.</summary>
    Tolerances,

    /// <summary>Manufacturing method, producibility and design for manufacture.</summary>
    Manufacturing,

    /// <summary>Surface condition, coating and treatment.</summary>
    SurfaceEngineering,

    /// <summary>Corrosion, temperature and other service-environment considerations.</summary>
    Environment,

    /// <summary>Sealing.</summary>
    Sealing,

    /// <summary>General mechanical design practice not specific to one element.</summary>
    GeneralMechanical,

    /// <summary>A domain this taxonomy does not classify. <see cref="RuleDefinition.SourceClassification"/> must then record the author's own wording.</summary>
    Other
}
