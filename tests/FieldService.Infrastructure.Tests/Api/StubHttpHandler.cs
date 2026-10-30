using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FieldService.Infrastructure.Tests.Api;

// Zapisane żądanie – migawka z chwili wysłania (ponowienie tego samego HttpRequestMessage daje osobny wpis)
public sealed class RecordedRequest
{
    public required HttpMethod Method { get; init; }
    public required string Path { get; init; }
    public required string Query { get; init; }
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
    public string? Body { get; init; }

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;

    public JsonElement Json() => JsonDocument.Parse(Body!).RootElement;
}

// Atrapa ostatniego ogniwa potoku HttpClient: zapisuje żądania i zwraca zadane odpowiedzi (po kolei;
// ostatnia zadana odpowiedź powtarza się dla kolejnych żądań) albo rzuca zadany wyjątek
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public RecordedRequest LastRequest => Requests[^1];

    public StubHttpHandler Respond(HttpStatusCode status, string? json = null, string contentType = "application/json",
        string? etag = null)
    {
        _responses.Enqueue(() =>
        {
            var response = new HttpResponseMessage(status);
            if (json is not null)
            {
                response.Content = new StringContent(json, Encoding.UTF8, contentType);
            }
            if (etag is not null)
            {
                response.Headers.ETag = new EntityTagHeaderValue(etag);
            }
            return response;
        });
        return this;
    }

    // odpowiedź binarna (np. obrazek PNG)
    public StubHttpHandler RespondBytes(HttpStatusCode status, byte[] bytes, string contentType = "image/png")
    {
        _responses.Enqueue(() => new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue(contentType) } }
        });
        return this;
    }

    public StubHttpHandler Throw(Exception exception)
    {
        _responses.Enqueue(() => throw exception);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        string? body = null;
        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        Requests.Add(new RecordedRequest
        {
            Method = request.Method,
            Path = request.RequestUri!.AbsolutePath,
            Query = request.RequestUri.Query,
            Headers = headers,
            Body = body,
        });

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("StubHttpHandler: brak zadanej odpowiedzi.");
        }

        var next = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
        return next();
    }
}
