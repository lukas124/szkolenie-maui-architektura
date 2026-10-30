using FieldService.Domain.Common;
using FieldService.Infrastructure.Api;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Api;

[TestFixture]
public class ApiErrorMapperTests
{
    // Tabela statusów bez pola "code" w treści – kody domyślne Api.*
    [TestCase(400, "Api.Validation", ErrorType.Validation)]
    [TestCase(422, "Api.Validation", ErrorType.Validation)]
    [TestCase(401, "Api.Unauthorized", ErrorType.Unauthorized)]
    [TestCase(404, "Api.NotFound", ErrorType.NotFound)]
    [TestCase(409, "Api.Conflict", ErrorType.Conflict, Ignore = "Ćwiczenie 13: mapowanie 409 w ApiErrorMapper")]
    [TestCase(428, "Api.Unexpected", ErrorType.Unexpected)]
    [TestCase(500, "Api.Unexpected", ErrorType.Unexpected)]
    [TestCase(502, "Api.Unexpected", ErrorType.Unexpected)]
    [TestCase(503, "Api.Unexpected", ErrorType.Unexpected)]
    [TestCase(403, "Api.Unexpected", ErrorType.Unexpected)]
    [TestCase(418, "Api.Unexpected", ErrorType.Unexpected)]
    public void Map_StatusWithoutCode_ReturnsDefaultError(int status, string expectedCode, ErrorType expectedType)
    {
        var error = ApiErrorMapper.Map(status, $"{{\"status\":{status},\"title\":\"Błąd\"}}");

        Assert.That(error, Is.EqualTo(new Error(expectedCode, expectedType)));
    }

    // 429 = chwilowy limit – traktujemy jak brak sieci (technik spróbuje ponownie)
    [Test]
    public void Map_429_ReturnsNetwork()
    {
        var error = ApiErrorMapper.Map(429, "{\"status\":429}");

        Assert.That(error, Is.EqualTo(ApiErrors.Network));
    }

    // Kod z pola "code" w ProblemDetails przechodzi dla 400, 422, 404 i 409
    [TestCase(400, "Visit.SignatureRequired", ErrorType.Validation)]
    [TestCase(422, "Visit.ReasonRequired", ErrorType.Validation)]
    [TestCase(404, "Visit.NotFound", ErrorType.NotFound)]
    [TestCase(409, "Inventory.OutOfStock", ErrorType.Conflict, Ignore = "Ćwiczenie 13: mapowanie 409 w ApiErrorMapper")]
    [TestCase(409, "Visit.VersionConflict", ErrorType.Conflict, Ignore = "Ćwiczenie 13: mapowanie 409 w ApiErrorMapper")]
    public void Map_StatusWithCode_ReturnsCodeFromResponse(int status, string code, ErrorType expectedType)
    {
        var error = ApiErrorMapper.Map(status, $"{{\"status\":{status},\"code\":\"{code}\"}}");

        Assert.That(error, Is.EqualTo(new Error(code, expectedType)));
    }

    // 409 z dodatkowymi polami (jak przy konflikcie wersji wizyty)
    [Test]
    [Ignore("Ćwiczenie 13: mapowanie 409 w ApiErrorMapper")]
    public void Map_409WithExtraFields_ReturnsCodeFromResponse()
    {
        const string response = "{\"type\":\"about:blank\",\"title\":\"Conflict\",\"status\":409," +
                                "\"code\":\"Visit.VersionConflict\",\"currentVersion\":7,\"currentStatus\":\"Cancelled\"}";

        var error = ApiErrorMapper.Map(409, response);

        Assert.That(error, Is.EqualTo(new Error("Visit.VersionConflict", ErrorType.Conflict)));
    }

    // 401 zawsze z kodem Api.Unauthorized – kod z treści nie zmienia znaczenia
    [Test]
    public void Map_401WithCode_ReturnsApiUnauthorized()
    {
        var error = ApiErrorMapper.Map(401, "{\"status\":401,\"code\":\"Auth.TokenExpired\"}");

        Assert.That(error, Is.EqualTo(new Error("Api.Unauthorized", ErrorType.Unauthorized)));
    }

    // 5xx z kodem – nadal Api.Unexpected
    [Test]
    public void Map_500WithCode_ReturnsApiUnexpected()
    {
        var error = ApiErrorMapper.Map(500, "{\"status\":500,\"code\":\"Server.Boom\"}");

        Assert.That(error, Is.EqualTo(new Error("Api.Unexpected", ErrorType.Unexpected)));
    }

    // Treść nie-JSON (np. strona HTML z proxy) – brak kodu, kod domyślny
    [TestCase(409, "Api.Conflict", ErrorType.Conflict, Ignore = "Ćwiczenie 13: mapowanie 409 w ApiErrorMapper")]
    [TestCase(404, "Api.NotFound", ErrorType.NotFound)]
    [TestCase(400, "Api.Validation", ErrorType.Validation)]
    public void Map_NonJsonResponse_ReturnsDefaultCode(int status, string expectedCode, ErrorType expectedType)
    {
        var error = ApiErrorMapper.Map(status, "<html><body>Bad gateway</body></html>");

        Assert.That(error, Is.EqualTo(new Error(expectedCode, expectedType)));
    }

    // Pusta treść i null – brak kodu, kod domyślny
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [Ignore("Ćwiczenie 13: mapowanie 409 w ApiErrorMapper")]
    public void Map_EmptyResponse_ReturnsDefaultCode(string? response)
    {
        var error = ApiErrorMapper.Map(409, response);

        Assert.That(error, Is.EqualTo(new Error("Api.Conflict", ErrorType.Conflict)));
    }

    // JSON bez obiektu (tablica) – brak kodu, bez wyjątku
    [Test]
    public void Map_JsonArrayResponse_ReturnsDefaultCode()
    {
        var error = ApiErrorMapper.Map(404, "[1,2,3]");

        Assert.That(error, Is.EqualTo(new Error("Api.NotFound", ErrorType.NotFound)));
    }
}
