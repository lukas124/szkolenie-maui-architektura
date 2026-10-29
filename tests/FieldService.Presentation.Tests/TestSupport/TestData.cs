using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Presentation.Tests.TestSupport;

// Jedno miejsce na założenia o API spoza kontraktu 09 (TestDoubles, fabryki Result) – patrz PYTANIA.md
internal static class TestData
{
    public static readonly DateTimeOffset Start = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    public static readonly Error NetworkError = new("Network.Unavailable", ErrorType.Conflict);

    public static Result<T> Ok<T>(T value) => Result<T>.Success(value);

    public static Result<T> Fail<T>(Error error) => Result<T>.Failure(error);

    // Wizyta w toku rozpoczęta o startedAt, zapisana w repozytorium
    public static VisitId SeedInProgress(InMemoryVisitRepository repository, DateTimeOffset startedAt)
    {
        var visit = TestVisits.Planned();
        visit.Start(startedAt);
        visit.ClearDomainEvents();
        repository.Add(visit);
        return visit.Id;
    }
}
