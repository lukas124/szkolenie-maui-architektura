using FieldService.Infrastructure.Auth;
using System.Net;
using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Infrastructure.Api;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Api;

[TestFixture]
public class ApiAuthGatewayTests
{
    private const string TokenJson =
        "{\"accessToken\":\"access-1\",\"refreshToken\":\"refresh-1\",\"expiresIn\":900,\"technicianName\":\"Jan Kowalski\"}";

    private StubHttpHandler _stub = null!;
    private ApiAuthGateway _gateway = null!;

    [TearDown]

    public void TearDown() => _stub.Dispose();


    [SetUp]
    public void SetUp()
    {
        _stub = new StubHttpHandler();
        var client = new FieldServiceApiClient(new HttpClient(_stub) { BaseAddress = new Uri("https://api.test/") });
        _gateway = new ApiAuthGateway(client, new ApiExecutor(NullLogger<ApiExecutor>.Instance));
    }

    // ---------- Logowanie ----------

    // POST api/auth/login z polami email i pin
    [Test]
    public async Task SignIn_SendsPostWithEmailAndPin()
    {
        _stub.Respond(HttpStatusCode.OK, TokenJson);

        await _gateway.SignInAsync("jan.kowalski@firma.pl", "1234", CancellationToken.None);

        var request = _stub.LastRequest;
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.Path, Does.EndWith("/api/auth/login"));
        var body = request.Json();
        Assert.That(body.GetProperty("email").GetString(), Is.EqualTo("jan.kowalski@firma.pl"));
        Assert.That(body.GetProperty("pin").GetString(), Is.EqualTo("1234"));
    }

    // 200 → SessionTokens z odpowiedzi
    [Test]
    public async Task SignIn_Success_ReturnsSessionTokens()
    {
        _stub.Respond(HttpStatusCode.OK, TokenJson);

        var result = await _gateway.SignInAsync("jan.kowalski@firma.pl", "1234", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new SessionTokens("access-1", "refresh-1", "Jan Kowalski")));
    }

    // 401 przy logowaniu = zły e-mail albo PIN
    [Test]
    public async Task SignIn_401_ReturnsInvalidCredentials()
    {
        _stub.Respond(HttpStatusCode.Unauthorized, "{\"status\":401,\"title\":\"Unauthorized\"}");

        var result = await _gateway.SignInAsync("jan.kowalski@firma.pl", "0000", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Auth.InvalidCredentials", ErrorType.Unauthorized)));
    }

    // 401 bez treści – też zły e-mail albo PIN
    [Test]
    public async Task SignIn_401WithoutBody_ReturnsInvalidCredentials()
    {
        _stub.Respond(HttpStatusCode.Unauthorized);

        var result = await _gateway.SignInAsync("jan.kowalski@firma.pl", "0000", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Auth.InvalidCredentials", ErrorType.Unauthorized)));
    }

    // Brak sieci → Network
    [Test]
    public async Task SignIn_HttpRequestException_ReturnsNetwork()
    {
        _stub.Throw(new HttpRequestException("brak sieci"));

        var result = await _gateway.SignInAsync("jan.kowalski@firma.pl", "1234", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // 429 (za dużo prób) → Network
    [Test]
    public async Task SignIn_429_ReturnsNetwork()
    {
        _stub.Respond(HttpStatusCode.TooManyRequests, "{\"status\":429}");

        var result = await _gateway.SignInAsync("jan.kowalski@firma.pl", "1234", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }

    // ---------- Odświeżenie ----------

    // POST api/auth/refresh z polem refreshToken
    [Test]
    public async Task Refresh_SendsPostWithRefreshToken()
    {
        _stub.Respond(HttpStatusCode.OK, TokenJson);

        await _gateway.RefreshAsync("refresh-0", CancellationToken.None);

        var request = _stub.LastRequest;
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.Path, Does.EndWith("/api/auth/refresh"));
        Assert.That(request.Json().GetProperty("refreshToken").GetString(), Is.EqualTo("refresh-0"));
    }

    // 200 → nowe tokeny (rotacja refresh tokenu)
    [Test]
    public async Task Refresh_Success_ReturnsSessionTokens()
    {
        _stub.Respond(HttpStatusCode.OK, TokenJson);

        var result = await _gateway.RefreshAsync("refresh-0", CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(new SessionTokens("access-1", "refresh-1", "Jan Kowalski")));
    }

    // 401 przy odświeżeniu → Api.Unauthorized (sesja wygasła)
    [Test]
    public async Task Refresh_401_ReturnsUnauthorized()
    {
        _stub.Respond(HttpStatusCode.Unauthorized, "{\"status\":401}");

        var result = await _gateway.RefreshAsync("refresh-0", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(new Error("Api.Unauthorized", ErrorType.Unauthorized)));
    }

    // Brak sieci przy odświeżeniu → Network
    [Test]
    public async Task Refresh_HttpRequestException_ReturnsNetwork()
    {
        _stub.Throw(new HttpRequestException("brak sieci"));

        var result = await _gateway.RefreshAsync("refresh-0", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ApiErrors.Network));
    }
}
