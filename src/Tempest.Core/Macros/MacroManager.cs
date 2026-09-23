using Tempest.Core.Logging;
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
    private readonly SettingsDocument<List<MacroDto>> _document;
    private readonly ICommandRegistry _commandRegistry;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ICommandMacro> _macrosById = new();
    private readonly HashSet<string> _registeredDescriptorIds = new(StringComparer.Ordinal);

    /// <summary>Initialises a new instance of the <see cref="MacroManager"/> class.</summary>
    public MacroManager(ISettingsProvider settingsProvider, ICommandRegistry commandRegistry, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _settingsProvider = settingsProvider;
        _commandRegistry = commandRegistry;

        _document = new SettingsDocument<List<MacroDto>>(settingsProvider, SettingKey, "User Command Macros", logger);
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var dtos = await _document.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (dtos is null)
            return;

        lock (_gate)
        {
            foreach (var dto in dtos)
            {
                // A structurally-valid list can still carry a corrupted
                // entry (null Name/StepCommandIds, or a null step Id,
                // after a partial write); one bad entry must not abort
                // loading the rest.
                if (dto.Name is null || dto.StepCommandIds is null
                    || dto.StepCommandIds.Any(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                var macro = new CommandMacro(dto.Id, dto.Name, BuildSteps(dto));
                _macrosById[macro.Id] = macro;
                RegisterDescriptorIfNeeded(macro);
            }
        }
    }

    /// <summary>
    /// Rebuilds each step's own recorded values from
    /// <see cref="MacroDto.StepValues"/>, positionally aligned with
    /// <see cref="MacroDto.StepCommandIds"/> — <see langword="null"/> for
    /// a macro persisted before recorded values existed, or for one whose
    /// list is shorter than the steps it describes (a partial write); a
    /// step past the end, or one <see cref="MacroDto.StepValues"/> itself
    /// records as <see langword="null"/>, records no values, exactly as
    /// every step did before this Work Package.
    /// </summary>
    private static List<MacroStep> BuildSteps(MacroDto dto)
    {
        var steps = new List<MacroStep>(dto.StepCommandIds!.Count);

        for (var i = 0; i < dto.StepCommandIds.Count; i++)
        {
            var id = dto.StepCommandIds[i];
            var recorded = dto.StepValues is { } values && i < values.Count ? values[i] : null;
            steps.Add(recorded is null ? new MacroStep(id) : new MacroStep(id, recorded));
        }

        return steps;
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
    public Task<ICommandMacro> CreateAsync(string name, IReadOnlyList<string> stepCommandIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stepCommandIds);

        return CreateAsync(name, stepCommandIds.Select(id => new MacroStep(id)).ToList(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ICommandMacro> CreateAsync(string name, IReadOnlyList<MacroStep> steps, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be null, empty, or whitespace.", nameof(name));

        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
            throw new ArgumentException("A macro must have at least one step.", nameof(steps));

        var registeredById = _commandRegistry.Items.ToDictionary(d => d.Id, StringComparer.Ordinal);

        foreach (var step in steps)
        {
            if (!registeredById.TryGetValue(step.CommandId, out var descriptor))
                throw new ArgumentException($"'{step.CommandId}' is not a registered command Id.", nameof(steps));

            // `WP 20.2C`, `ADR-0099`'s own addendum: a command this
            // platform genuinely cannot invoke yet (today, the
            // object-picker set — `WorkspaceCommandBindings.ObjectPickerRequired`)
            // is refused here, with its own declared reason, rather than
            // accepted and left to fail unexplained the first time the
            // macro runs.
            if (descriptor.Binding is { IsInvocable: false } binding)
            {
                throw new ArgumentException(
                    $"'{step.CommandId}' cannot be a macro step: {binding.UnavailableReason}", nameof(steps));
            }
        }

        var macro = new CommandMacro(Guid.NewGuid(), name, steps);

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

            // `WP 20.2C`: the descriptor comes down with the macro — see
            // `ICommandRegistry.Unregister`'s own remarks for why this is
            // now safe (`Items` is read fresh by every consumer, not
            // cached).
            var descriptorId = IMacroManager.CommandIdPrefix + id;
            _registeredDescriptorIds.Remove(descriptorId);
            _commandRegistry.Unregister(descriptorId);
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
            description: $"Runs {macro.Steps.Count} step(s) in sequence.",
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
                .Select(m => new MacroDto(
                    m.Id,
                    m.Name,
                    m.Steps.Select(s => s.CommandId).ToList(),
                    m.Steps.Select(s => s.RecordedValues.Count > 0
                        ? new Dictionary<string, string>(s.RecordedValues, StringComparer.Ordinal)
                        : null)
                        .ToList()))
                .ToList();
        }

        await _document.SaveAsync(dtos, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The plain, JSON-serializable shape one macro persists as.
    /// </summary>
    /// <param name="StepValues">
    /// Each step's own recorded values, positionally aligned with
    /// <paramref name="StepCommandIds"/> — <see langword="null"/> entries
    /// where a step recorded none (`WP 20.2C`). Optional, and additive:
    /// a macro persisted by an earlier build carries no
    /// <c>"StepValues"</c> property at all, which deserializes to
    /// <see langword="null"/> here — the exact shape every macro had
    /// before this Work Package (verified: <c>System.Text.Json</c> honours
    /// a record parameter's own default when the JSON property is
    /// absent).
    /// </param>
    private sealed record MacroDto(
        Guid Id, string Name, List<string> StepCommandIds, List<Dictionary<string, string>?>? StepValues = null);
}
