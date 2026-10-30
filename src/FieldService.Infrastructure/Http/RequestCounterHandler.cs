using Microsoft.Extensions.Logging;

namespace FieldService.Infrastructure.Http;

// Pomiar: każda odpowiedź z serwera – status i bajty treści (Content-Length, a gdy brak – długość treści).
// W potoku za ETagCacheHandler, więc widzi prawdziwe 304; przed odpornością (Polly) – ponowienie liczy się jako jedno
// żądanie. Rejestrowany tylko w Debug i Test (CachingOptions.CountRequests).
public sealed class RequestCounterHandler(RequestStatistics stats, ILogger<RequestCounterHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);
        var bytes = response.Content.Headers.ContentLength ?? (await response.Content.ReadAsByteArrayAsync(ct)).LongLength;
        stats.Add((int)response.StatusCode, bytes);
        logger.LogInformation("http.requests {Path} {Status} {Bytes}", request.RequestUri?.AbsolutePath, (int)response.StatusCode, bytes);
        return response;
    }
}
