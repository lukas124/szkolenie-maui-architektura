using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using FieldService.Infrastructure.Persistence;

namespace FieldService.Infrastructure.Http;

// Dekorator potoku: odpowiedź GET z ETagiem zapamiętana w SQLite. Następny GET pyta „czy coś się zmieniło?”
// (If-None-Match). 304 = nie – oddajemy zapamiętaną treść jako zwykłe 200, więc wygenerowany klient nic nie wie,
// a przez sieć idą same nagłówki. RFC 9110: ETag, If-None-Match, 304 Not Modified.
public sealed class ETagCacheHandler(SqliteDb db) : DelegatingHandler
{
    private sealed class CacheRow
    {
        public string ETag { get; set; } = "";
        public string Body { get; set; } = "";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri is null) return await base.SendAsync(request, ct);

        var url = request.RequestUri.AbsoluteUri;
        var connection = await db.GetConnectionAsync();
        var cached = (await connection.QueryAsync<CacheRow>("SELECT ETag, Body FROM http_cache WHERE Url = ?", url)).FirstOrDefault();
        // ponowienie tego samego żądania (np. po odświeżeniu tokenu) – nagłówek ustawiamy od nowa, nie dokładamy
        request.Headers.IfNoneMatch.Clear();
        if (cached is not null) request.Headers.IfNoneMatch.ParseAdd(cached.ETag);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
        {
            response.Dispose();
            return FromCache(request, cached);
        }

        // tylko JSON (listy, katalog) – treść zapamiętujemy jako tekst
        if (response.StatusCode == HttpStatusCode.OK && response.Headers.ETag is { } etag
            && response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            await connection.ExecuteAsync("INSERT OR REPLACE INTO http_cache (Url, ETag, Body, StoredAt) VALUES (?, ?, ?, ?)",
                url, etag.ToString(), body, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
        return response;
    }

    private static HttpResponseMessage FromCache(HttpRequestMessage request, CacheRow cached)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(cached.Body, System.Text.Encoding.UTF8, "application/json")
        };
        response.Headers.ETag = EntityTagHeaderValue.Parse(cached.ETag);
        return response;
    }
}
