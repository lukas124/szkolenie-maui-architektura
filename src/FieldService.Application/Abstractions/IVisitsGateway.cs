using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Abstractions;

// Port (brama do API wizyt): kształt definiuje przypadek użycia, implementacja jest w Infrastructure
public interface IVisitsGateway
{
    Task<Result<IReadOnlyList<VisitSummary>>> GetTodayAsync(CancellationToken ct);

    // Wizyta z serwera: stan (model) + migawka danych do wyświetlenia
    Task<Result<RemoteVisit>> GetVisitAsync(VisitId id, CancellationToken ct);

    // Historia wizyt technika – jedna strona
    Task<Result<VisitHistoryPageResult>> GetHistoryAsync(int page, int pageSize, CancellationToken ct);
}
