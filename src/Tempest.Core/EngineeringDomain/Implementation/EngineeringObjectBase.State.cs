using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// <see cref="EngineeringObjectBase"/>'s state half: capturing this
/// object's whole state as a value, and applying one back to it.
/// </summary>
/// <remarks>
/// <para>
/// Split from the mutators deliberately. Since `ADR-0145` those two halves
/// answer different questions — the mutators own <em>when</em> a change is
/// durable, and this file owns <em>what</em> an object's state consists of
/// — and they change for different reasons: a new facet field changes this
/// file, a change to the write path changes the other.
/// </para>
/// <para>
/// <see cref="EngineeringObjectBase.CaptureState"/> and
/// <see cref="EngineeringObjectBase.RestoreState"/> are the single
/// definition of "this object's state" (`ADR-0113`), shared by
/// persistence, rehydration and revision alike, so a field added here
/// cannot be honoured by one path and forgotten by another.
/// </para>
/// </remarks>
public abstract partial class EngineeringObjectBase
{
    /// <summary>
    /// Captures this object's own complete state (`TD-85`) — everything
    /// that must come back after a restart for this to be the same object.
    /// Always stamped with
    /// <see cref="EngineeringObjectStateStore.CurrentSchemaVersion"/>
    /// (`TD-87`, `ADR-0120`).
    /// </summary>
    internal EngineeringObjectState CaptureState()
    {
        var typeState = new Dictionary<string, string?>(StringComparer.Ordinal);
        CaptureTypeState(typeState);

        lock (_lifecycleLock)
        {
            lock (_structuralLock)
            {
                return new EngineeringObjectState(
                    EngineeringObjectStateStore.CurrentSchemaVersion,
                    Id,
                    Kind,
                    Identifier,
                    _displayName,
                    Metadata,
                    _status,
                    _parentId,
                    _isDeleted,
                    new EngineeringObjectBomLineState(_quantity, _unitOfMeasure, _findNumber, _itemNumber, _referenceDesignator),
                    _history.Select(h => new EngineeringObjectTransitionState(h.From, h.To, h.ActorPrincipalId, h.OccurredAt, h.ApprovalId)).ToList(),
                    CaptureAttachmentState(),
                    typeState);
            }
        }
    }

    /// <summary>
    /// Projects <c>_attachments</c> under the monitor its own writers use
    /// (`WP 16.4B-R5`) — taken innermost, inside <c>_lifecycleLock</c> and
    /// <c>_structuralLock</c>, and never held while either of those is
    /// acquired, so no lock-order inversion is introduced.
    /// </summary>
    private List<EngineeringObjectAttachmentState> CaptureAttachmentState()
    {
        lock (_attachments)
        {
            return _attachments
                .Select(a => new EngineeringObjectAttachmentState(a.Id, a.FileName, a.ContentType, a.SizeInBytes, a.ContentHash))
                .ToList();
        }
    }

    /// <summary>
    /// Restores the mutable state a constructor cannot carry (`TD-85`) —
    /// applied immediately after reconstructing an instance, so the object
    /// is fully itself before any caller can observe it.
    /// </summary>
    /// <remarks>
    /// Identifier, display name, metadata and <b>every type-specific
    /// field</b> arrive through the Kind's own
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> constructor, which is
    /// the reader for the <see cref="EngineeringObjectState.TypeState"/>
    /// half of the record; this restores what lives in mutable base fields
    /// instead. It deliberately does not read
    /// <see cref="EngineeringObjectState.TypeState"/>: there is no
    /// base-class writer for a type's own fields, only that type's own
    /// constructor and its <see cref="ApplyTypeState"/> override.
    /// </remarks>
    internal void RestoreState(EngineeringObjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        ApplyBaseState(state, attachments: null);
    }

    /// <summary>
    /// Writes a committed <paramref name="state"/> into this instance —
    /// the third and last step of every mutator (`ADR-0145`).
    /// </summary>
    /// <remarks>
    /// Called only after the transaction that made <paramref name="state"/>
    /// durable has committed, so this instance and its record cannot
    /// disagree. <paramref name="attachments"/> lets an attach path keep
    /// the caller's own <see cref="IAttachment"/> instances rather than
    /// swapping them for equal-valued copies rebuilt from the record;
    /// everywhere else it is <see langword="null"/> and the record is the
    /// only source.
    /// </remarks>
    private void ApplyCommittedState(EngineeringObjectState state, IReadOnlyList<IAttachment>? attachments = null)
    {
        ApplyBaseState(state, attachments);
        ApplyTypeState(state);
    }

    private void ApplyBaseState(EngineeringObjectState state, IReadOnlyList<IAttachment>? attachments)
    {
        lock (_lifecycleLock)
        {
            _status = state.Status;
            _history.Clear();
            foreach (var transition in state.History)
                _history.Add(new LifecycleTransitionRecord(transition.From, transition.To, transition.ActorPrincipalId, transition.OccurredAt, transition.ApprovalId));
        }

        lock (_attachments)
        {
            _attachments.Clear();

            if (attachments is not null)
            {
                _attachments.AddRange(attachments);
            }
            else
            {
                foreach (var attachment in state.Attachments)
                    _attachments.Add(new Attachment(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeInBytes, attachment.ContentHash));
            }
        }

        lock (_structuralLock)
        {
            _displayName = state.DisplayName;
            _parentId = state.ParentId;
            _isDeleted = state.IsDeleted;
            _quantity = state.BomLine.Quantity;
            _unitOfMeasure = state.BomLine.UnitOfMeasure;
            _findNumber = state.BomLine.FindNumber;
            _itemNumber = state.BomLine.ItemNumber;
            _referenceDesignator = state.BomLine.ReferenceDesignator;
        }
    }

    /// <summary>
    /// Writes this concrete type's own state into <paramref name="state"/>
    /// (`TD-85`). A type with fields beyond the shared facets overrides
    /// this and writes them; its own <see cref="IRehydratable{TSelf}.Rehydrate"/>
    /// reads them back.
    /// </summary>
    protected virtual void CaptureTypeState(IDictionary<string, string?> state)
    {
    }

    /// <summary>
    /// Reads this concrete type's own fields back out of a <b>committed</b>
    /// <paramref name="state"/> — the writer that pairs with
    /// <see cref="CaptureTypeState"/> for an object that is already live
    /// (`ADR-0145`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Kind whose own mutators go through
    /// <see cref="MutateTypeStateAndPersistAsync"/> overrides this so the
    /// committed value reaches its fields; a Kind with no mutators of its
    /// own does not need to, because its type state never changes after
    /// construction. <see cref="IRehydratable{TSelf}.Rehydrate"/> remains
    /// the reader for a <em>new</em> instance; this is the reader for an
    /// existing one, and the two must agree.
    /// </para>
    /// <para>
    /// Called only from <see cref="ApplyCommittedState"/>, i.e. only after
    /// the state it is given is durable.
    /// </para>
    /// </remarks>
    protected virtual void ApplyTypeState(EngineeringObjectState state)
    {
    }

    /// <summary>Writes a list of values into type state, as JSON.</summary>
    protected static void WriteList(IDictionary<string, string?> state, string key, IEnumerable<string>? values) =>
        state[key] = values is null ? null : System.Text.Json.JsonSerializer.Serialize(values.ToList());

    /// <summary>Writes a list of <see cref="Guid"/> values into type state, as JSON.</summary>
    protected static void WriteGuidList(IDictionary<string, string?> state, string key, IEnumerable<Guid>? values) =>
        WriteList(state, key, values?.Select(v => v.ToString()));

    /// <summary>Writes an arbitrary serialisable value into type state, as JSON — for a type whose own field is neither a scalar nor a list of scalars.</summary>
    protected static void WriteJson<TValue>(IDictionary<string, string?> state, string key, TValue? value) =>
        state[key] = value is null ? null : System.Text.Json.JsonSerializer.Serialize(value);
}
