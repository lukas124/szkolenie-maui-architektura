using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;

namespace FieldService.TestDoubles;

public sealed record StoredPhoto(VisitId VisitId, string FilePath, DateTimeOffset TakenAt);

// Fałszywka magazynu zdjęć: pamięta dodane zdjęcia i liczy je per wizyta.
public sealed class FakeVisitPhotoStore : IVisitPhotoStore
{
    private readonly List<StoredPhoto> _photos = [];

    public IReadOnlyList<StoredPhoto> Photos => _photos;

    public Task AddAsync(VisitId visitId, string filePath, DateTimeOffset takenAt, CancellationToken ct)
    {
        _photos.Add(new StoredPhoto(visitId, filePath, takenAt));
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(VisitId visitId, CancellationToken ct) =>
        Task.FromResult(_photos.Count(p => p.VisitId == visitId));
}
