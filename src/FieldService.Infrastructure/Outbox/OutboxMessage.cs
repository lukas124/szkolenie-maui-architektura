using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using FieldService.Domain.Common;
using FieldService.Domain.Visits.Events;

namespace FieldService.Infrastructure.Outbox;

// Wiersz tabeli outbox: zmiana wizyty do wysłania na serwer. Zapisywana w tej samej transakcji co wizyta.
public sealed class OutboxMessage
{
    // polskie znaki bez \u…, czytelne w podglądzie tabeli (ciało żądania JSON, nie HTML)
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // klucz idempotencji (nagłówek Idempotency-Key) – nadany raz, przy zapisie; ten sam przy każdym ponowieniu
    public string Id { get; set; } = "";

    // nazwa zdarzenia – po niej brama wybiera żądanie
    public string Type { get; set; } = "";
    public string VisitId { get; set; } = "";

    // JSON ciała żądania
    public string Payload { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    // null = jeszcze niewysłana
    public string? SentAt { get; set; }

    // nie wysyłamy więcej (za dużo nieudanych prób albo konflikt z serwerem)
    public bool Failed { get; set; }

    // Zdarzenie domenowe → wiadomość. Nie wysyłamy: unieważnienia podpisu (serwer unieważnia go sam po zmianie zakresu)
    // i anulowania (anuluje dyspozytor, nie technik).
    public static OutboxMessage? From(IDomainEvent domainEvent) => domainEvent switch
    {
        VisitStarted e => Create(nameof(VisitStarted), e.VisitId.Value, e.OccurredAt, new { startedAt = e.OccurredAt }),
        ActivityRecorded e => Create(nameof(ActivityRecorded), e.VisitId.Value, e.OccurredAt,
            new { code = e.Activity.Code, description = e.Activity.Description, recordedAt = e.Activity.RecordedAt }),
        PartUsed e => Create(nameof(PartUsed), e.VisitId.Value, e.OccurredAt,
            new { partId = e.PartId.Value, quantity = e.Quantity.Value }),
        PartRemoved e => Create(nameof(PartRemoved), e.VisitId.Value, e.OccurredAt, new { partId = e.PartId.Value }),
        CustomerSigned e => Create(nameof(CustomerSigned), e.VisitId.Value, e.OccurredAt,
            new { signerName = e.SignerName, signedAt = e.OccurredAt }),
        VisitCompleted e => Create(nameof(VisitCompleted), e.VisitId.Value, e.OccurredAt,
            new { outcome = e.Outcome.Type.ToString(), reason = e.Outcome.Reason, completedAt = e.OccurredAt }),
        _ => null
    };

    private static OutboxMessage Create(string type, Guid visitId, DateTimeOffset occurredAt, object payload) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Type = type,
        VisitId = visitId.ToString(),
        Payload = JsonSerializer.Serialize(payload, Json),
        CreatedAt = occurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
    };
}
