using System.Text.Json;
using FieldService.Domain.Common;
using Microsoft.Extensions.Logging;
using Polly;

namespace FieldService.Infrastructure.Api;

// Wykonanie wokół (Execute Around Method): jedno miejsce przed i po każdym wywołaniu API. Odpowiedź i wyjątki
// (także wyjątki Polly) → Result. Anulowanie przez wywołującego idzie dalej. Nie pokazuje UI i nie czeka na użytkownika.
// Bramy nie mają ani jednego try/catch.
public sealed class ApiExecutor(ILogger<ApiExecutor> logger)
{
    public async Task<Result<T>> ExecuteAsync<T>(string operation, Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        try
        {
            return Result<T>.Success(await call(ct));
        }
        catch (ApiException ex) // serwer odpowiedział statusem błędu (wygenerowany klient rzuca ApiException)
        {
            return Fail<T>(operation, ex, ToError(ex));
        }
        // wyjątki sieci i niepoprawna odpowiedź → błąd w Result
        catch (HttpRequestException ex) // brak sieci, DNS, zerwane połączenie
        {
            return Fail<T>(operation, ex, ApiErrors.Network);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) // limit czasu HttpClient, nie anulowanie
        {
            return Fail<T>(operation, ex, ApiErrors.Network);
        }
        catch (ExecutionRejectedException ex) // Polly: limit czasu próby/całości albo otwarty obwód
        {
            return Fail<T>(operation, ex, ApiErrors.Network);
        }
        catch (JsonException ex) // treść nie jest oczekiwanym JSON-em (gdy klient nie opakował błędu w ApiException)
        {
            return Fail<T>(operation, ex, ApiErrors.InvalidResponse);
        }
    }

    public async Task<Result> ExecuteAsync(string operation, Func<CancellationToken, Task> call, CancellationToken ct)
    {
        var result = await ExecuteAsync(operation, async c => { await call(c); return true; }, ct);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error!);
    }

    private Result<T> Fail<T>(string operation, Exception ex, Error error)
    {
        logger.LogWarning("{Operation} → {ErrorCode} ({Exception})", operation, error.Code, ex.GetType().Name);
        return Result<T>.Failure(error);
    }

    private static Error ToError(ApiException ex) => ex switch
    {
        // odpowiedź 2xx, której nie dało się odczytać (np. strona portalu logowania Wi-Fi zamiast JSON-a) – to nie brak sieci
        { StatusCode: >= 200 and < 300 } => ApiErrors.InvalidResponse,
        // odpowiedź opisana w specyfikacji jako ProblemDetails: klient czyta ją do obiektu (Response bywa puste)
        ApiException<ProblemDetails> { Result: { } problem } when string.IsNullOrEmpty(ex.Response) =>
            ApiErrorMapper.Map(ex.StatusCode, JsonSerializer.Serialize(problem)),
        _ => ApiErrorMapper.Map(ex.StatusCode, ex.Response)
    };
}
