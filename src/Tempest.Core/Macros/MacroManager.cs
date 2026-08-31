using System.Text.Json;
using Tempest.Core.Commands;
using Tempest.Core.Settings;

namespace Tempest.Core.Macros;

/// <summary>The concrete <see cref="IMacroManager"/> implementation — persists via <see cref="ISettingsProvider"/>, mirroring <c>Tempest.Desktop.UserSettings</c>'s own established JSON-DTO pattern, applied here at the Platform Service layer instead of a Desktop-local one, since a macro (unlike a UI preference) is meaningfully cross-presentation.</summary>
public sealed class MacroManager : IMacroManager
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this state is stored under.</summary>
    public const string SettingKey = "Core.Macros";

    private readonly ISettingsProvider _settingsProvider;
    private readonly ICommandRegistry _commandRegistry;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ICommandMacro> _macrosById = new();
    private readonly HashSet<string> _registeredDescriptorIds = new(StringComparer.Ordinal);

    /// <summary>Initialises a new instance of the <see cref="MacroManager"/> class.</summary>
    public MacroManager(ISettingsProvider settingsProvider, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _settingsProvider = settingsProvider;
        _commandRegistry = commandRegistry;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(SettingKey, "User Command Macros", string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Already registered by a prior instance against the same
            // ISettingsProvider (a restart) — idempotent, mirroring
            // UserSettings'/DesktopPanelUiState's own identical discipline.
        }
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsProvider.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);

        List<MacroDto>? dtos;
        try
        {
            dtos = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<MacroDto>>(json);
        }
        catch (JsonException)
        {
            // A corrupted stored value (e.g. a torn write) degrades to
            // "no persisted macros" rather than failing the load (`TD-60`).
            dtos = null;
        }

        if (dtos is null)
            return;

        lock (_gate)
        {
            foreach (var dto in dtos)
            {
                // A structurally-valid list can still carry a corrupted
                // entry (null Name/StepCommandIds after a partial write);
                // one bad entry must not abort loading the rest.
                if (dto.Name is null || dto.StepCommandIds is null)
                    continue;

                var macro = new CommandMacro(dto.Id, dto.Name, dto.StepCommandIds);
                _macrosById[macro.Id] = macro;
                RegisterDescriptorIfNeeded(macro);
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ICommandMacro>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ICommandMacro> result = _macrosById.Values
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<ICommandMacro?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_macrosById.TryGetValue(id, out var macro) ? macro : null);
        }
    }

    /// <inheritdoc />
    public async Task<ICommandMacro> CreateAsync(string name, IReadOnlyList<string> stepCommandIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be null, empty, or whitespace.", nameof(name));

        ArgumentNullException.ThrowIfNull(stepCommandIds);

        if (stepCommandIds.Count == 0)
            throw new ArgumentException("A macro must have at least one step.", nameof(stepCommandIds));

        var registeredIds = _commandRegistry.Items.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var unknownStep = stepCommandIds.FirstOrDefault(id => !registeredIds.Contains(id));
        if (unknownStep is not null)
            throw new ArgumentException($"'{unknownStep}' is not a registered command Id.", nameof(stepCommandIds));

        var macro = new CommandMacro(Guid.NewGuid(), name, stepCommandIds);

        lock (_gate)
        {
            _macrosById[macro.Id] = macro;
            RegisterDescriptorIfNeeded(macro);
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);

        return macro;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_macrosById.Remove(id))
                return;
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers <paramref name="macro"/>'s own <see cref="CommandDescriptor"/>
    /// against <see cref="_commandRegistry"/> if not already registered
    /// this process — <see cref="ICommandRegistry.RegisterDescriptor"/>
    /// throws <see cref="DuplicateCommandIdException"/> on a repeat Id, so
    /// this tracks what this instance has itself already registered
    /// rather than relying on catching that exception as control flow.
    /// Must be called under <see cref="_gate"/>.
    /// </summary>
    private void RegisterDescriptorIfNeeded(ICommandMacro macro)
    {
        var descriptorId = IMacroManager.CommandIdPrefix + macro.Id;
        if (!_registeredDescriptorIds.Add(descriptorId))
            return;

        var macroId = macro.Id;
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            descriptorId,
            macro.Name,
            category: "Macros",
            description: $"Runs {macro.StepCommandIds.Count} step(s) in sequence.",
            createDefault: () => new RunMacroCommand(macroId))
        {
            // TD-77 Stage 5. CreateDefault is kept exactly as it was, so
            // every caller that already invoked a macro by bare Id still
            // does. The binding is what lets a surface hand the macro the
            // selection the person had when they started it, which its own
            // steps then replay.
            //
            // It requires nothing: a macro with nothing selected is a valid
            // thing to run, and its steps report for themselves what they
            // needed. MultipleAllowed because a macro is not a single-target
            // command and must not be refused merely because two objects
            // happen to be selected.
            Binding = new CommandBinding(
                CommandContextRequirement.MultipleAllowed,
                (context, _) => new RunMacroCommand(macroId, context)),
        });
    }

    /// <summary>Writes the current macro set via <see cref="ISettingsProvider.SetValueAsync"/>.</summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        List<MacroDto> dtos;
        lock (_gate)
        {
            dtos = _macrosById.Values
                .Select(m => new MacroDto(m.Id, m.Name, m.StepCommandIds.ToList()))
                .ToList();
        }

        var json = JsonSerializer.Serialize(dtos);
        await _settingsProvider.SetValueAsync(SettingKey, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The plain, JSON-serializable shape one macro persists as.</summary>
    private sealed record MacroDto(Guid Id, string Name, List<string> StepCommandIds);
}
