using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The one concrete type behind every Structural, Electrical and Building
/// Services / HVAC Kind (`ADR-0163`). The Kind string says what the object
/// is; <see cref="Attributes"/> carries what the engineer recorded about it.
/// </summary>
public sealed class DisciplineObject : EngineeringObjectBase, IDisciplineObject, IRehydratable<DisciplineObject>
{
    /// <summary>The type-state key prefix every attribute is stored under.</summary>
    private const string AttributePrefix = "Attribute:";

    private IReadOnlyDictionary<string, string> _attributes;

    /// <summary>Initialises a new instance of the <see cref="DisciplineObject"/> class.</summary>
    public DisciplineObject(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        IReadOnlyDictionary<string, string>? attributes = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        _attributes = new Dictionary<string, string>(attributes ?? new Dictionary<string, string>(), StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    /// <inheritdoc />
    public Task SetAttributeAsync(string name, string? value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();
        var trimmedValue = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        return MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [AttributePrefix + trimmedName] = trimmedValue },
            () =>
            {
                var next = new Dictionary<string, string>(_attributes, StringComparer.Ordinal);
                if (trimmedValue is null)
                    next.Remove(trimmedName);
                else
                    next[trimmedName] = trimmedValue;
                _attributes = next;
            },
            trimmedValue is null ? $"Cleared '{trimmedName}'." : $"Set '{trimmedName}' to '{trimmedValue}'.",
            cancellationToken);
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        foreach (var (name, value) in _attributes)
            state[AttributePrefix + name] = value;
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state) => _attributes = ReadAttributes(state);

    private static Dictionary<string, string> ReadAttributes(EngineeringObjectState state)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in state.TypeState)
        {
            if (key.StartsWith(AttributePrefix, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(value))
                attributes[key[AttributePrefix.Length..]] = value;
        }

        return attributes;
    }

    static DisciplineObject IRehydratable<DisciplineObject>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, ReadAttributes(state));
}
