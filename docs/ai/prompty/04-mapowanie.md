# Prompt 04 – mapowanie DTO

**Cel:** dostać mapowanie formatu API na model aplikacji (Data Mapper w warstwie ochronnej, ang. Anti-Corruption
Layer) razem z testem, także dla danych, których model się nie spodziewa.

**Kontekst do dołączenia:**
- `AGENTS.md`;
- `src/FieldService.Infrastructure/Visits/VisitDto.cs`, `VisitDetailsDto.cs`, `VisitDtoMapper.cs`;
- typy docelowe: `src/FieldService.Application/Visits/Queries/GetTodayVisits.cs` (`VisitSummary`),
  `src/FieldService.Domain/Visits/VisitStatus.cs`;
- przykładowa odpowiedź API (bez danych osobowych – wartości zmyślone).

## Prompt

```text
Napisz <metodę mapowania, np. VisitDtoMapper.ToSummary> i testy w
tests/FieldService.Infrastructure.Tests/<katalog>/<Nazwa>Tests.cs.

Przypadki testów:
1. poprawny DTO → wszystkie pola modelu;
2. <nieznana wartość z API, np. status "Rescheduled"> → <oczekiwane zachowanie>, bez wyjątku;
3. <pole opcjonalne puste> → <oczekiwane zachowanie>.

Zasady: DTO nie wychodzi poza Infrastructure; mapowanie nie rzuca wyjątków dla danych z API;
nie dodawaj pakietów (bez bibliotek mapujących). Klienta HTTP nie generuj – tylko mapowanie.
```

## Czego oczekiwać

Statyczną metodę mapującą i 3 testy. Jeśli LLM użyje `Enum.Parse`, test nieznanej wartości będzie czerwony –
wtedy kolejny prompt: „test jest czerwony – popraw implementację, nie test”.

## Jak przejrzeć

- Czy test nieznanej wartości istnieje i przechodzi?
- Czy typy DTO są `internal` i zostają w `FieldService.Infrastructure`?
- `git diff --stat tests/` po poprawce – test bez zmian.
- Klient API z OpenAPI generuje deterministyczny generator w buildzie, nie LLM.
