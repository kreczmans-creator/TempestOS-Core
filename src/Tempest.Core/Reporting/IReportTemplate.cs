namespace Tempest.Core.Reporting;

/// <summary>
/// A reusable layout for turning a <typeparamref name="TDefinition"/>'s
/// own report data into its final rendered output — separates "what
/// data a report needs" (a renderer's own business logic) from "how
/// that data is laid out and rendered" (this template).
/// </summary>
/// <remarks>
/// <para>
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft (which named only <see cref="IReportDefinition"/>,
/// <see cref="IReportRenderer{TDefinition}"/>, and
/// <see cref="IReportingService"/>) — an additive elaboration this Work
/// Package's own implementation phase introduces, mirroring `WP 6.1`'s
/// own <c>IRole</c>/<c>IIdentityService</c>, `WP 6.4`'s own
/// <c>SettingDefinition</c>, and `WP 6.2`'s own
/// <c>IPlatformNotification</c> precedent: filling a gap this Work
/// Package's own implementation brief named ("Template abstraction,"
/// "Separate: Report data, Report layout, Rendering pipeline") but the
/// architecture package never drafted as an interface member, without
/// changing any approved <see cref="IReportingService"/>/<see cref="IReportRenderer{TDefinition}"/>
/// shape.
/// </para>
/// <para>
/// Entirely optional — a renderer implementation may apply a template
/// internally (as an ordinary constructor-injected collaborator) or may
/// render its own output directly; <see cref="IReportingService"/> has
/// no awareness of templates at all, so this abstraction can evolve, or
/// gain new implementations, without ever touching the approved
/// dispatch contract.
/// </para>
/// </remarks>
/// <typeparam name="TDefinition">The report definition type this template renders.</typeparam>
public interface IReportTemplate<TDefinition> where TDefinition : IReportDefinition
{
    /// <summary>The MIME content type this template produces.</summary>
    string ContentType { get; }

    /// <summary>
    /// Applies this template to <paramref name="data"/> — the report's
    /// own business data, already gathered by a renderer — producing
    /// the final rendered output.
    /// </summary>
    /// <param name="definition">The report definition being rendered.</param>
    /// <param name="request">The parameters for this specific generation request.</param>
    /// <param name="data">The report's own data, as simple key/value pairs.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="definition"/>, <paramref name="request"/>, or
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    ReportResult Apply(TDefinition definition, ReportRequest request, IReadOnlyDictionary<string, string> data);
}
