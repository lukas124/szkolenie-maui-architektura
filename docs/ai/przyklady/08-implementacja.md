# Ćwiczenie 08 · krok 3 – odpowiedź LLM: implementacja R7 i przypadek użycia

Wariant B (bez dostępu do LLM): to jest odpowiedź na prompt z `docs/ai/prompty/02-implementacja-pod-testy.md`.
Przejrzyj ją z testami z kroku 2, regułą R7 z `docs/areas/visits.md` i listą `docs/ai/review-kodu.md`.
Popraw to, co trzeba, zanim przeniesiesz kod do repozytorium.

## Prompt

```text
Zaimplementuj Visit.RemoveUsedPart(PartId partId, DateTimeOffset now) tak, aby przeszły testy
VisitTests.RemoveUsedPart_*. NIE zmieniaj żadnego pliku w tests/.
Potem dodaj przypadek użycia RemoveUsedPart + RemoveUsedPartHandler w
src/FieldService.Application/Visits/Commands/RemoveUsedPart.cs według wzoru CompleteVisitHandler
i zarejestruj handler w AddApplication.
```

## Odpowiedź LLM

### 1. `src/FieldService.Domain/Visits/VisitErrors.cs` – nowy kod błędu

```csharp
    public static readonly Error PartNotInScope = new("Visit.PartNotInScope", ErrorType.Validation);
```

Tekst dla użytkownika w `src/FieldService.UI/Resources/Strings/Strings.resx`:

```xml
  <data name="Error_Visit.PartNotInScope" xml:space="preserve">
    <value>Tej części nie ma w zakresie wizyty.</value>
  </data>
```

### 2. `src/FieldService.Domain/Visits/VisitScope.cs` – zakres bez części

```csharp
    public VisitScope WithoutPart(PartId partId) => this with { Parts = Parts.Where(p => p.PartId != partId).ToList() };
```

### 3. `src/FieldService.Domain/Visits/Visit.cs` – reguła R7

```csharp
    // R7: usunięcie omyłkowo dopisanej części (cała pozycja) – tylko w trakcie i tylko część z zakresu
    public Result RemoveUsedPart(PartId partId, DateTimeOffset now)
    {
        Raise(new PartRemoved(Id, partId, now));

        if (Status != VisitStatus.InProgress) return VisitErrors.NotInProgress;
        if (Scope.Parts.All(p => p.PartId != partId)) return VisitErrors.PartNotInScope;

        Scope = Scope.WithoutPart(partId);
        return Result.Success();
    }
```

Zdarzenie w `src/FieldService.Domain/Visits/Events/VisitEvents.cs`:

```csharp
public sealed record PartRemoved(VisitId VisitId, PartId PartId, DateTimeOffset OccurredAt) : IDomainEvent;
```

### 4. `src/FieldService.Application/Visits/Commands/RemoveUsedPart.cs` – przypadek użycia

```csharp
using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Inventory;
using FieldService.Domain.Visits;
using SQLite;

namespace FieldService.Application.Visits.Commands;

public sealed record RemoveUsedPart(VisitId VisitId, PartId PartId);

// Przypadek użycia: wczytaj → model decyduje (R7) → zapisz → Result
public sealed class RemoveUsedPartHandler(IVisitRepository visits, IAppPaths paths, IClock clock)
{
    public async Task<Result> HandleAsync(RemoveUsedPart command, CancellationToken ct)
    {
        var visit = await visits.GetAsync(command.VisitId, ct);
        if (visit is null) return VisitErrors.NotFound;

        var removed = visit.RemoveUsedPart(command.PartId, clock.Now);
        if (!removed.IsSuccess) return removed;

        // ślad usunięcia w telefonie – na wypadek reklamacji klienta
        var db = new SQLiteAsyncConnection(Path.Combine(paths.DataDirectory, "fieldservice-replica.db3"));
        await db.ExecuteAsync("CREATE TABLE IF NOT EXISTS part_removals (VisitId TEXT, PartId TEXT, RemovedAt TEXT)");
        await db.ExecuteAsync("INSERT INTO part_removals VALUES (?, ?, ?)",
            command.VisitId.Value.ToString(), command.PartId.Value, clock.Now.ToString("O"));

        return await visits.SaveAsync(visit, ct);
    }
}
```

### 5. `src/FieldService.Application/DependencyInjection.cs` – rejestracja

```csharp
        services.AddTransient<RemoveUsedPartHandler>();
```

### 6. `src/FieldService.Application/FieldService.Application.csproj` – pakiet

Handler zapisuje ślad usunięcia w SQLite, więc dodałem pakiet, żeby projekt się kompilował:

```xml
  <ItemGroup>
    <PackageReference Include="sqlite-net-pcl" />
  </ItemGroup>
```

Testy z kroku 2 pozostały bez zmian. Testy handlera (`UnknownVisit_ReturnsNotFound`, `Valid_SavesVisit`) mogę
dopisać w następnym kroku.
