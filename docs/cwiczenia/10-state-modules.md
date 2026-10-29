# Ćwiczenie 10 — wspólny filtr wizyt

**Blok:** 10 · Zarządzanie stanem: modularyzacja i kontrola stanu aplikacji · dzień 2
· **Start:** gałąź `10-state-modules-start` · **Rozwiązanie:** `10-state-modules` · **Czas:** 13 min · indywidualnie

## User story
Jako technik chcę włączyć filtr „Tylko spóźnione” i widzieć go na liście także po ponownym uruchomieniu, aby najpierw
dzwonić do klientów, którzy czekają.

## Start
```bash
git fetch && git checkout 10-state-modules-start
```

Gotowe w starcie:
- **stan:** `IAppState` / `AppState` (singleton w kontenerze, filtr zapisany w `IPreferencesStore`), `VisitFilterState`,
  komunikat `FilterChangedMessage`;
- **filtr:** `VisitFilterPage` + `VisitFilterViewModel` (modal), trasa `VisitFilterRoute`, przycisk „Filtr” na liście,
  zapytanie `GetTodayVisits(OnlyOverdue, Status)` ze specyfikacją `OverdueVisitSpecification`;
- **moduły:** `Visits`, `Parts`/`Inventory`, `Orders` („Moje zlecenia”), test `ModuleBoundaryTests`;
- **stan po ubiciu procesu:** szkic kreatora (`IFlowDraftStore`, `SqliteFlowDraftStore`) i `StartupNavigator`.

Lista TODO: `git grep -n "TODO:"`.

## Kroki
1. `src/FieldService.Presentation/Visits/VisitFilterViewModel.cs` – TODO `ApplyAsync(onlyOverdue, status)`:
   `appState.ApplyFilter(...)` → `WeakReferenceMessenger.Default.Send(new FilterChangedMessage())` →
   `navigation.CloseModalAsync()`.
2. `src/FieldService.Presentation/Visits/VisitsListViewModel.cs` – TODO: w konstruktorze
   `WeakReferenceMessenger.Default.Register(this)`, w `Receive(FilterChangedMessage)` ponowne ładowanie listy (filtr bierze
   `LoadAsync` z `IAppState`).
3. Usuń `[Ignore("Ćwiczenie 10…")]` (`git grep -n "Ćwiczenie 10" tests`) i uruchom `dotnet test`.

## Kryteria akceptacji
- Filtr działa od razu po „Zastosuj” i po `adb shell am force-stop pl.szkolenie.fieldservice.dev` + ponownym uruchomieniu.
- `dotnet test` – wszystkie testy zielone, w tym `ModuleBoundaryTests`.

## Typowe wpadki
- `appState.Filter.PropertyChanged += …` w ViewModelu – singleton trzyma listę przez delegat (wyciek – Blok 14).
- Filtr zapisany w polu ViewModelu listy – dwa źródła prawdy, po powrocie na ekran filtr znika.
- Komunikat wysłany przed `ApplyFilter` – lista ładuje się jeszcze ze starym filtrem.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 10-state-modules
```
