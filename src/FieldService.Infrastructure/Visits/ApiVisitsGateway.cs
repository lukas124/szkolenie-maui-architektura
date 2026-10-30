using System.Text.Json;
using FieldService.Application.Abstractions;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Api;
using FieldService.Infrastructure.Outbox;

namespace FieldService.Infrastructure.Visits;

// Adapter portu IVisitsGateway na wygenerowanym kliencie: wywołanie przez ApiExecutor (Result), DTO → model przez
// VisitDtoMapper (ACL). Wysyła też zmiany z outbox: wiadomość → komenda z wersją i kluczem idempotencji.
internal sealed class ApiVisitsGateway(FieldServiceApiClient client, ApiExecutor executor) : IVisitsGateway, IOutboxSender
{
    public async Task<Result<IReadOnlyList<VisitSummary>>> GetTodayAsync(CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("visits.today", client.GetTodayVisitsAsync, ct);
        if (!result.IsSuccess) return result.Error!;

        IReadOnlyList<VisitSummary> visits = result.Value.Select(VisitDtoMapper.ToSummary).ToList();
        return Result<IReadOnlyList<VisitSummary>>.Success(visits);
    }

    public async Task<Result<RemoteVisit>> GetVisitAsync(VisitId id, CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("visits.get", c => client.GetVisitAsync(id.Value, c), ct);
        if (!result.IsSuccess) return result.Error!.Type == ErrorType.NotFound ? VisitErrors.NotFound : result.Error!;

        return Result<RemoteVisit>.Success(VisitDtoMapper.ToRemoteVisit(result.Value));
    }

    public async Task<Result<VisitHistoryPageResult>> GetHistoryAsync(int page, int pageSize, CancellationToken ct)
    {
        var result = await executor.ExecuteAsync("visits.history", c => client.GetVisitHistoryAsync(page, pageSize, c), ct);
        if (!result.IsSuccess) return result.Error!;

        return Result<VisitHistoryPageResult>.Success(VisitDtoMapper.ToHistoryPage(result.Value));
    }

    // Mapa wiadomość → komenda: typ zdarzenia wybiera metodę klienta, Payload to ciało; wersja w If-Match i X-If-Match
    // (proxy hostingu wycina If-Match), klucz idempotencji = Id wiadomości
    public async Task<Result<int>> SendAsync(OutboxMessage message, int expectedVersion, CancellationToken ct)
    {
        var id = Guid.Parse(message.VisitId);
        var version = $"\"{expectedVersion}\"";
        var key = message.Id;
        Func<CancellationToken, Task<VisitDetailsDto>>? call = message.Type switch
        {
            "VisitStarted" => c => client.StartVisitAsync(id, version, version, key, Body<StartVisitRequest>(message), c),
            "ActivityRecorded" => c => client.RecordActivityAsync(id, version, version, key, Body<RecordActivityRequest>(message), c),
            "PartUsed" => c => client.UsePartAsync(id, version, version, key, Body<UsePartRequest>(message), c),
            "PartRemoved" => c => client.RemoveUsedPartAsync(id, Body<UsePartRequest>(message).PartId, version, version, key, c),
            "CustomerSigned" => c => client.SignVisitAsync(id, version, version, key, Body<SignVisitRequest>(message), c),
            "VisitCompleted" => c => client.CompleteVisitAsync(id, version, version, key, Body<CompleteVisitRequest>(message), c),
            _ => null
        };
        if (call is null) return new Error("Api.Unexpected", ErrorType.Unexpected);

        var result = await executor.ExecuteAsync($"visits.send.{message.Type}", call, ct);
        return result.IsSuccess ? Result<int>.Success(result.Value.Version) : result.Error!;
    }

    // Payload zapisany przy zapisie wizyty ma pola ciała żądania (camelCase jak w specyfikacji)
    private static T Body<T>(OutboxMessage message) => JsonSerializer.Deserialize<T>(message.Payload)!;
}
