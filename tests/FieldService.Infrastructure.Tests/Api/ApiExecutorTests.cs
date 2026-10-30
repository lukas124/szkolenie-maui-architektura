using System.Text.Json;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Api;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace FieldService.Infrastructure.Tests.Api;

[TestFixture]
public class ApiExecutorTests
{
    private ApiExecutor _executor = null!;

    [SetUp]
    public void SetUp() => _executor = new ApiExecutor(NullLogger<ApiExecutor>.Instance);

    private static ApiException ApiError(int status, string response) =>
        new("Błąd HTTP", status, response, new Dictionary<string, IEnumerable<string>>(), null);

    // Sukces – wynik wywołania w Result
    [Test]
    public async Task Execute_Success_ReturnsValue()
    {
        var result = await _executor.ExecuteAsync("test.op", _ => Task.FromResult(42), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(42));
    }

    // Wywołanie dostaje CancellationToken przekazany do ExecuteAsync
    [Test]
    public async Task Execute_PassesCancellationTokenToCall()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken received = default;

        await _executor.ExecuteAsync("test.op", c => { received = c; return Task.FromResult(1); }, cts.Token);

        Assert.That(received, Is.EqualTo(cts.Token));
    }

    // ApiException z kodem w treści → ApiErrorMapper (409 Inventory.OutOfStock)
    [Test]
    public async Task Execute_ApiException409_ReturnsMappedConflict()
    {
        var result = await _executor.ExecuteAsync<int>("test.op",
            _ => throw ApiError(409, "{\"status\":409,\"code\":\"Inventory.OutOfStock\"}"), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(new Error("Inventory.OutOfStock", ErrorType.Conflict)));
    }

    // ApiException – status przechodzi przez ApiErrorMapper
    [TestCase(401, "Api.Unauthorized", ErrorType.Unauthorized)]
    [TestCase(404, "Api.NotFound", ErrorType.NotFound)]
    [TestCase(422, "Api.Validation", ErrorType.Validation)]
    [TestCase(500, "Api.Unexpected", ErrorType.Unexpected)]
    public async Task Execute_ApiException_ReturnsMappedError(int status, string expectedCode, ErrorType expectedType)
    {
        var result = await _executor.ExecuteAsync<int>("test.op", _ => throw ApiError(status, ""), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error(expectedCode, expectedType)));
    }

    // ApiException 429 → Network
    [Test]
    public async Task Execute_ApiException429_ReturnsNetwork()
    {
        var result = await _executor.ExecuteAsync<int>("test.op", _ => throw ApiError(429, ""), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // ApiException ze statusem 2xx = wygenerowany klient nie odczytał treści → InvalidResponse
    [TestCase(200)]
    [TestCase(201)]
    public async Task Execute_ApiExceptionWithSuccessStatus_ReturnsInvalidResponse(int status)
    {
        var result = await _executor.ExecuteAsync<int>("test.op",
            _ => throw ApiError(status, "<html>portal logowania Wi-Fi</html>"), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.InvalidResponse));
    }

    // Brak sieci / DNS
    [Test]
    public async Task Execute_HttpRequestException_ReturnsNetwork()
    {
        var result = await _executor.ExecuteAsync<int>("test.op",
            _ => throw new HttpRequestException("brak sieci"), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // Limit czasu HttpClient (TaskCanceledException), gdy wywołujący nie anulował
    [Test]
    public async Task Execute_TaskCanceledWithoutCancellation_ReturnsNetwork()
    {
        using var cts = new CancellationTokenSource();

        var result = await _executor.ExecuteAsync<int>("test.op",
            _ => throw new TaskCanceledException("timeout"), cts.Token);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // Anulowanie przez wywołującego idzie dalej jako wyjątek
    [Test]
    public void Execute_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await _executor.ExecuteAsync<int>("test.op",
                c => throw new TaskCanceledException("anulowano", null, c), cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    // Anulowanie w trakcie wywołania (OperationCanceledException) – też wyjątek, nie Network
    [Test]
    public void Execute_CancelledDuringCall_Throws()
    {
        using var cts = new CancellationTokenSource();

        Assert.That(async () => await _executor.ExecuteAsync<int>("test.op", c =>
            {
                cts.Cancel();
                c.ThrowIfCancellationRequested();
                return Task.FromResult(1);
            }, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    // Wyjątki Polly (limit czasu próby/całości, otwarty obwód) → Network
    [Test]
    public async Task Execute_PollyRejected_ReturnsNetwork()
    {
        var timeout = await _executor.ExecuteAsync<int>("test.op",
            _ => throw new TimeoutRejectedException(), CancellationToken.None);
        var broken = await _executor.ExecuteAsync<int>("test.op",
            _ => throw new BrokenCircuitException(), CancellationToken.None);

        Assert.That(timeout.Error, Is.EqualTo(ApiErrors.Network));
        Assert.That(broken.Error, Is.EqualTo(ApiErrors.Network));
    }

    // Nieczytelna treść odpowiedzi
    [Test]
    public async Task Execute_JsonException_ReturnsInvalidResponse()
    {
        var result = await _executor.ExecuteAsync<int>("test.op",
            _ => throw new JsonException("zła treść"), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.InvalidResponse));
    }

    // Wariant bez wyniku – sukces
    [Test]
    public async Task ExecuteWithoutValue_Success_ReturnsSuccess()
    {
        var called = false;

        var result = await _executor.ExecuteAsync("test.op", _ => { called = true; return Task.CompletedTask; },
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(called, Is.True);
    }

    // Wariant bez wyniku – ApiException mapowany jak w wariancie z wynikiem
    [Test]
    public async Task ExecuteWithoutValue_ApiException404_ReturnsNotFound()
    {
        var result = await _executor.ExecuteAsync("test.op",
            _ => throw ApiError(404, "{\"status\":404,\"code\":\"Reservation.NotFound\"}"), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(new Error("Reservation.NotFound", ErrorType.NotFound)));
    }

    // Wariant bez wyniku – brak sieci
    [Test]
    public async Task ExecuteWithoutValue_HttpRequestException_ReturnsNetwork()
    {
        var result = await _executor.ExecuteAsync("test.op",
            _ => throw new HttpRequestException("brak sieci"), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // Wariant bez wyniku – anulowanie idzie dalej
    [Test]
    public void ExecuteWithoutValue_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await _executor.ExecuteAsync("test.op",
                c => throw new OperationCanceledException(c), cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
