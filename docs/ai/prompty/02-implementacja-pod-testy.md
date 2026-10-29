# Prompt 02 – implementacja pod testy

**Cel:** dostać kod, który spełnia przejrzane testy z promptu 01, bez ich zmiany.

**Kontekst do dołączenia:**
- `AGENTS.md`, `src/FieldService.Domain/AGENTS.md`;
- testy z kroku 1 (plik albo diff);
- `src/FieldService.Domain/Visits/Visit.cs`, `VisitScope.cs`, `VisitErrors.cs`, `Events/VisitEvents.cs`;
- wzór przypadku użycia: `src/FieldService.Application/Visits/Commands/CompleteVisit.cs` i jego test
  `tests/FieldService.Application.Tests/Visits/CompleteVisitHandlerTests.cs`.

## Prompt

```text
Zaimplementuj <Visit.Metoda(parametry)> tak, aby przeszły testy <nazwy testów albo VisitTests.Metoda_*>.
NIE zmieniaj żadnego pliku w tests/ poza dopisaniem nowych testów handlera.

Potem dodaj przypadek użycia <NazwaKomendy> + <NazwaKomendy>Handler w
src/FieldService.Application/Visits/Commands/<NazwaKomendy>.cs według wzoru CompleteVisitHandler:
wczytaj z IVisitRepository → nieznane Id: VisitErrors.NotFound → metoda modelu z clock.Now →
błąd: zwróć go bez zapisu → sukces: SaveAsync. Zarejestruj handler w AddApplication.
Dopisz testy handlera: UnknownVisit_ReturnsNotFound, Valid_SavesVisit (InMemoryVisitRepository, FixedClock).

Zasady: Result zamiast wyjątków; czas tylko z IClock; zdarzenie zgłaszane dopiero po sprawdzeniu reguł;
zmiana zakresu przez ScopeChanged (R3); bez nowych pakietów i bez nowych referencji między projektami;
nowy kod błędu → wpis Error_{Code} w src/FieldService.UI/Resources/Strings/Strings.resx.
```

## Czego oczekiwać

Metoda w `Visit`, ewentualnie metoda w `VisitScope`, nowy kod w `VisitErrors`, plik komendy z handlerem, jedna
linia w `src/FieldService.Application/DependencyInjection.cs`, testy handlera i wpis w `Strings.resx`.

## Jak przejrzeć

- `git diff --stat tests/` – zmienione tylko nowe testy handlera, testy z kroku 1 bez zmian.
- Handler dostaje porty przez konstruktor (`IVisitRepository`, `IClock`), nie tworzy adapterów (`new Sqlite…`).
- `git diff -- '*.csproj'` – pusty.
- Lista [`../review-kodu.md`](../review-kodu.md), potem `dotnet test` (wszystkie projekty).
