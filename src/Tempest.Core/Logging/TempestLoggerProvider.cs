using MEL = Microsoft.Extensions.Logging;

namespace Tempest.Core.Logging;

/// <summary>
/// Forwards <c>Microsoft.Extensions.Logging</c> log messages into this
/// platform's own <see cref="ILogger"/>/<see cref="ILogSink"/> pipeline
/// (`WP 17.2A`, ADR-0146).
/// </summary>
/// <remarks>
/// <para>
/// Implements both <see cref="MEL.ILoggerProvider"/> and
/// <see cref="MEL.ILoggerFactory"/> over the same
/// <see cref="ILoggerFactory.CreateLogger"/> call, so a single instance is
/// registered into the DI container under
/// <see cref="MEL.ILoggerFactory"/> — the type any future library code
/// (an accounting connector's <c>HttpClient</c> diagnostics in
/// <c>v0.19.0</c>, SQLite's own logging hooks) asks for by convention — and
/// every message it writes lands in the exact same
/// <see cref="ConsoleLogSink"/>/<see cref="RollingFileLogSink"/> pair this
/// platform's own code already logs into, category-for-category. No
/// separate <c>Microsoft.Extensions.Logging</c> (non-Abstractions) package
/// is needed for this: this class <i>is</i> the factory, rather than a
/// provider registered into Microsoft's own composite
/// <c>LoggerFactory.Create</c>, which would pull that package in for no
/// behavioural benefit.
/// </para>
/// <para>
/// <see cref="MEL.ILoggerFactory.AddProvider"/> is a deliberate no-op: this
/// factory is not itself a composite of arbitrary providers a caller might
/// add — it always forwards into the one platform sink pipeline it was
/// constructed with.
/// </para>
/// </remarks>
public sealed class TempestLoggerProvider : MEL.ILoggerProvider, MEL.ILoggerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Initialises a new instance of the <see cref="TempestLoggerProvider"/> class.</summary>
    /// <param name="loggerFactory">The platform's own logger factory every forwarded message is ultimately written through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
    public TempestLoggerProvider(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc cref="MEL.ILoggerProvider.CreateLogger" />
    public MEL.ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);

        return new BridgeLogger(_loggerFactory.CreateLogger(categoryName));
    }

    /// <inheritdoc />
    void MEL.ILoggerFactory.AddProvider(MEL.ILoggerProvider provider)
    {
        // See this class's own remarks: deliberately a no-op.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Owns no resource of its own — _loggerFactory's own sink(s) are
        // owned and disposed by whoever constructed them (TempestHost).
    }

    /// <summary>Adapts one platform <see cref="ILogger"/> to <see cref="MEL.ILogger"/>.</summary>
    private sealed class BridgeLogger : MEL.ILogger
    {
        private readonly ILogger _inner;

        public BridgeLogger(ILogger inner) => _inner = inner;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(MEL.LogLevel logLevel) => logLevel != MEL.LogLevel.None;

        public void Log<TState>(
            MEL.LogLevel logLevel,
            MEL.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);

            switch (Map(logLevel))
            {
                case LogLevel.Trace:
                    _inner.Trace(message, exception);
                    break;
                case LogLevel.Debug:
                    _inner.Debug(message, exception);
                    break;
                case LogLevel.Information:
                    _inner.Information(message, exception);
                    break;
                case LogLevel.Warning:
                    _inner.Warning(message, exception);
                    break;
                case LogLevel.Error:
                    _inner.Error(message, exception);
                    break;
                case LogLevel.Critical:
                    _inner.Critical(message, exception);
                    break;
            }
        }

        private static LogLevel Map(MEL.LogLevel level) => level switch
        {
            MEL.LogLevel.Trace => LogLevel.Trace,
            MEL.LogLevel.Debug => LogLevel.Debug,
            MEL.LogLevel.Information => LogLevel.Information,
            MEL.LogLevel.Warning => LogLevel.Warning,
            MEL.LogLevel.Error => LogLevel.Error,
            MEL.LogLevel.Critical => LogLevel.Critical,
            _ => LogLevel.None,
        };

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            private NullScope()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
