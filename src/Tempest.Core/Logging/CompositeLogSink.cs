namespace Tempest.Core.Logging;

/// <summary>
/// An <see cref="ILogSink"/> that fans a log entry out to two or more
/// child sinks.
/// </summary>
/// <remarks>
/// <para>
/// Closes the single-sink limitation disclosed since <c>WP 2.6</c>
/// (<c>Technical Debt Register.md</c>, TD-02) — a consumer of
/// <see cref="ILogger"/>/<see cref="ILoggerFactory"/> requires no change
/// of any kind to benefit from more than one sink; only the sink handed
/// to <see cref="LoggerFactory"/> changes, from a single
/// <see cref="ConsoleLogSink"/> to a <see cref="CompositeLogSink"/>
/// wrapping it alongside any other <see cref="ILogSink"/> implementation.
/// </para>
/// <para>
/// Each child sink's own failure is isolated: caught, reported directly
/// to <see cref="Console.Error"/> — bypassing the failed sink, never the
/// remaining ones — and never allowed to prevent a sibling sink from
/// receiving the same entry. This mirrors <see cref="Logger"/>'s own
/// established sink-failure-isolation convention exactly (see that
/// class's remarks), applied here one level down so that a single
/// failing child cannot silently swallow delivery to every sink after
/// it in the list.
/// </para>
/// </remarks>
public sealed class CompositeLogSink : ILogSink
{
    private readonly IReadOnlyList<ILogSink> _sinks;

    /// <summary>
    /// Initialises a new instance of the <see cref="CompositeLogSink"/> class.
    /// </summary>
    /// <param name="sinks">
    /// The child sinks to fan a log entry out to, in the order they should
    /// be written.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="sinks"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sinks"/> is empty, or contains a <see langword="null"/> entry.
    /// </exception>
    public CompositeLogSink(IEnumerable<ILogSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);

        var materialised = sinks.ToList();

        if (materialised.Count == 0)
            throw new ArgumentException("At least one sink must be supplied.", nameof(sinks));

        if (materialised.Any(sink => sink is null))
            throw new ArgumentException("Sinks must not contain a null entry.", nameof(sinks));

        _sinks = materialised;
    }

    /// <summary>
    /// Gets the child sinks this composite fans a log entry out to, in
    /// the order they are written.
    /// </summary>
    public IReadOnlyList<ILogSink> Sinks => _sinks;

    /// <inheritdoc />
    /// <remarks>
    /// Writes <paramref name="entry"/> to every child sink, in order. A
    /// child sink's own exception is caught and reported to
    /// <see cref="Console.Error"/>; it never prevents a later sink in the
    /// list from receiving the same entry, and never propagates to this
    /// method's own caller.
    /// </remarks>
    public void Write(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        foreach (var sink in _sinks)
        {
            try
            {
                sink.Write(entry);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[CompositeLogSink] Sink '{sink.GetType().Name}' failed while writing a log entry: {ex}");
            }
        }
    }
}
