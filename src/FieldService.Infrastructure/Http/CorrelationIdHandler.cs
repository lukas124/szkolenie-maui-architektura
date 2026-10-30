namespace FieldService.Infrastructure.Http;

// Identyfikator korelacji (Hohpe & Woolf): każde żądanie z własnym X-Correlation-Id – ten sam identyfikator w logach
// aplikacji i serwera. W Bloku 15 zastąpi go nagłówek traceparent (OpenTelemetry).
public sealed class CorrelationIdHandler : DelegatingHandler
{
    public const string Header = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Remove(Header);
        request.Headers.Add(Header, Guid.NewGuid().ToString());
        return base.SendAsync(request, ct);
    }
}
