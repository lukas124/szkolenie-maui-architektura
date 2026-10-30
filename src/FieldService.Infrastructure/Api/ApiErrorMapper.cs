using System.Text.Json;
using FieldService.Domain.Common;

namespace FieldService.Infrastructure.Api;

// Status HTTP + ProblemDetails (pole "code") → Error. Jedno miejsce, w którym „urzędowy” język HTTP staje się naszym.
public static class ApiErrorMapper
{
    public static Error Map(int statusCode, string? response)
    {
        var code = CodeOf(response);
        return statusCode switch
        {
            400 or 422 => new Error(code ?? "Api.Validation", ErrorType.Validation),
            401 => new Error("Api.Unauthorized", ErrorType.Unauthorized),
            404 => new Error(code ?? "Api.NotFound", ErrorType.NotFound),
            // TODO: Ćwiczenie 13 – 409 → Conflict z kodem z treści (Inventory.OutOfStock, Visit.VersionConflict),
            //   bez kodu – "Api.Conflict" (wzór: 404)
            // limit żądań serwera – chwilowy, jak brak sieci
            429 => ApiErrors.Network,
            // 428 (brak If-Match) to błąd w kodzie aplikacji, nie sytuacja technika
            _ => new Error("Api.Unexpected", ErrorType.Unexpected)
        };
    }

    // stabilny kod odmowy z ProblemDetails, np. „Inventory.OutOfStock”; brak albo nie-JSON → null
    private static string? CodeOf(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        try
        {
            using var json = JsonDocument.Parse(response);
            return json.RootElement.ValueKind == JsonValueKind.Object
                   && json.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
