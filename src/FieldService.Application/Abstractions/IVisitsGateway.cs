using FieldService.Application.Visits.Queries;
using FieldService.Domain.Common;

namespace FieldService.Application.Abstractions;

// Port (brama do API wizyt): kształt definiuje przypadek użycia, implementacja jest w Infrastructure
public interface IVisitsGateway
{
    Task<Result<IReadOnlyList<VisitSummary>>> GetTodayAsync(CancellationToken ct);
}
