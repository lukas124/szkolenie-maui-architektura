namespace FieldService.Infrastructure.Persistence;

// Wiersz tabeli visits: stan wizyty (agregat) + migawka do wyświetlenia (klient, urządzenie)
internal sealed class VisitRow
{
    public string Id { get; set; } = "";
    public string WorkOrderId { get; set; } = "";
    public string TechnicianIdsJson { get; set; } = "[]";
    public string Status { get; set; } = "";
    public string PlannedStart { get; set; } = "";
    public string PlannedEnd { get; set; } = "";
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string ScopeJson { get; set; } = "";
    public int ScopeVersion { get; set; }
    public string? SignatureJson { get; set; }
    public string? OutcomeType { get; set; }
    public string? OutcomeReason { get; set; }
    public string? CancelReason { get; set; }
    public int Version { get; set; }

    // wersja z serwera (ostatnie pobranie albo odpowiedź na wysłaną zmianę) – idzie w If-Match przy wysyłce z outbox
    public int ServerVersion { get; set; }

    public string WorkOrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string Address { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string FaultDescription { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
}
