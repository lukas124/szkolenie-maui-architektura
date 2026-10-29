using FieldService.Domain.Visits;

namespace FieldService.Application.Abstractions;

// Port: zdjęcia wizyty – poza agregatem (żadna reguła ich nie potrzebuje, a są duże)
public interface IVisitPhotoStore
{
    Task AddAsync(VisitId visitId, string filePath, DateTimeOffset takenAt, CancellationToken ct);

    Task<int> CountAsync(VisitId visitId, CancellationToken ct);
}
