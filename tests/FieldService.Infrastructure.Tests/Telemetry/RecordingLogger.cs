using Microsoft.Extensions.Logging;

namespace FieldService.Infrastructure.Tests.Telemetry;

// Atrapa ILogger<T>: zapisuje poziom, stan (pary klucz–wartość), wyjątek i aktywne zakresy (BeginScope)
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<object> _scopes = [];

    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        _scopes.Add(state);
        return new ScopeHandle(() => _scopes.Remove(state));
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(
            logLevel,
            ToPairs(state),
            exception,
            formatter(state, exception),
            _scopes.Select(ToPairs).ToList()));
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> ToPairs(object? state) => state switch
    {
        IEnumerable<KeyValuePair<string, object?>> pairs => pairs.ToList(),
        IEnumerable<KeyValuePair<string, string>> texts => texts
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value)).ToList(),
        _ => []
    };

    private sealed class ScopeHandle(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

public sealed record LogEntry(
    LogLevel Level,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    Exception? Exception,
    string Message,
    IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> Scopes)
{
    // wartość atrybutu – najpierw ze stanu wpisu, potem z zakresów
    public object? Value(string key) =>
        State.Concat(Scopes.SelectMany(s => s))
            .Where(p => p.Key == key)
            .Select(p => p.Value)
            .FirstOrDefault();

    public bool Has(string key) => State.Concat(Scopes.SelectMany(s => s)).Any(p => p.Key == key);
}
