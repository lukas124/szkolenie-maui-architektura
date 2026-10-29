---
name: nowy-przypadek-uzycia
description: Nowy przypadek użycia (komenda wizyty) w FieldService od testu – reguła w agregacie, handler, rejestracja, ViewModel. Użyj, gdy dochodzi nowa czynność technika na wizycie.
---

# Nowy przypadek użycia – od testu

1. **Reguła w modelu – najpierw test.** W `tests/FieldService.Domain.Tests/Visits/` testy given–when–then nowej metody
   `Visit` (sukces, każda odmowa z kodem z `VisitErrors`, zdarzenie, wpływ na podpis – R3). Uruchom
   `dotnet test tests/FieldService.Domain.Tests` – testy mają być czerwone. Pokaż je człowiekowi do przeglądu.
2. **Metoda agregatu.** W `src/FieldService.Domain/Visits/Visit.cs`: sprawdzenie reguł → zmiana stanu → zdarzenie
   (`Events/VisitEvents.cs`) → `Result`. Nowy błąd w `VisitErrors.cs`. Testów z kroku 1 nie zmieniaj.
3. **Handler.** W `src/FieldService.Application/Visits/Commands/<Nazwa>.cs` rekord komendy i `<Nazwa>Handler` według
   wzoru `CompleteVisit.cs`: wczytaj → `VisitErrors.NotFound` → metoda agregatu z `clock.Now` → przy błędzie bez
   zapisu → `visits.SaveAsync`. Testy w `tests/FieldService.Application.Tests/Visits/<Nazwa>HandlerTests.cs`
   z `InMemoryVisitRepository` i `FixedClock` (co najmniej: nieznana wizyta, odmowa bez zapisu, zapis).
4. **Rejestracja.** `services.AddTransient<<Nazwa>Handler>();` w `src/FieldService.Application/DependencyInjection.cs`.
5. **ViewModel.** Komenda w `src/FieldService.Presentation/Visits/VisitDetailsViewModel.cs` przez `RunAsync(...)`;
   test w `tests/FieldService.Presentation.Tests`. Tekst błędu `Error_Visit.<Nazwa>` w `Strings.resx`.
6. **Sprawdzenie.** `dotnet build FieldService.slnx` (zero ostrzeżeń), `dotnet test FieldService.slnx` (wszystko zielone,
   także `FieldService.Tests`), `git diff --stat tests/` – testy z kroku 1 bez zmian; lista `docs/ai/review-kodu.md`.
