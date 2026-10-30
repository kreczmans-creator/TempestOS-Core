using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library.Dashboard;

/// <summary>
/// Addendum C integration (C-4 with C-3): the dashboard's "Records not entered" column from C-3's statement of what each calculator cannot do
/// without (<see cref="RecordRequirements"/>, the same table behind <see cref="RecordImportService.MissingRequiredAsync"/> and the worksheet's
/// missing-records panel). For a calculator that table names, the row lists the record kinds it marks <see cref="RecordNeed.Required"/>; a kind it
/// runs on a default for is not "missing". For a calculator the table does not name, every record kind the template declares is listed, as
/// <see cref="StoreRequiredRecordsSource"/> does. Material kinds are left out either way (the library ships generic starting materials, and C-3
/// counts the material library as present).
/// </summary>
/// <remarks>
/// What "entered" means stays C-4's rule: a record counts only if it is usable for real work (not Indicative, not a SYNTHETIC test table,
/// <see cref="StoreRequiredRecordsSource.IsUsableForRealWork"/>); for a kind C-3 states, it must also hold a value the calculator takes (C-3's
/// rule, <see cref="RecordImportService.HoldsValueFor"/>). It only reads the store, once per report; unlike
/// <see cref="RecordImportService.RequirementsAsync"/> it never seeds the material library, because the dashboard writes nothing.
/// </remarks>
public sealed class RecordRequirementsRecordsSource : IRequiredRecordsSource
{
    private readonly CalcInstanceStore _store;

    public RecordRequirementsRecordsSource(CalcInstanceStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Whether C-3's requirements table names the calculator <paramref name="templateId"/> (its rows then show only what it marks Required).</summary>
    public static bool IsStated(string templateId) => RecordRequirements.Entries.Any(e => string.Equals(e.Template, templateId, StringComparison.Ordinal));

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<DashboardRecordRequirement>>> RequirementsAsync(IReadOnlyList<CalcTemplate> templates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var usable = (await _store.ListRecordsAsync(null, cancellationToken).ConfigureAwait(false))
            .Where(StoreRequiredRecordsSource.IsUsableForRealWork)
            .ToLookup(r => r.Kind, StringComparer.Ordinal);
        var result = new Dictionary<string, IReadOnlyList<DashboardRecordRequirement>>(StringComparer.Ordinal);

        foreach (var template in templates)
        {
            var kinds = (template.RecordKinds ?? []).Where(k => !MaterialLibrary.IsMaterial(k.Kind));

            result[template.Id] = IsStated(template.Id)
                ?
                [
                    .. kinds
                        .Where(k => RecordRequirements.For(template.Id, k.Kind).Need == RecordNeed.Required)
                        .Select(k => new DashboardRecordRequirement(k.Kind, k.Title, usable[k.Kind].Any(r => RecordImportService.HoldsValueFor(r, k)))),
                ]
                : [.. kinds.Select(k => new DashboardRecordRequirement(k.Kind, k.Title, usable[k.Kind].Any()))];
        }

        return result;
    }
}
