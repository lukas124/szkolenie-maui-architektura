# Ćwiczenie 09 — nawigacja przez trasy i kreator bez lokalizatora usług

**Blok:** 09 · Wstrzykiwanie zależności i nawigacja: kontenery IoC, NavigationPage i routing · dzień 2
· **Start:** gałąź `09-navigation-start` · **Rozwiązanie:** `09-navigation` · **Czas:** 19 min · indywidualnie

## User story
Jako technik chcę z listy wejść w wizytę i jednym przyciskiem otworzyć kreator zakończenia, a po zapisie wrócić na
odświeżoną listę, aby kończyć wizyty bez szukania ich ponownie.

## Start
```bash
git fetch && git checkout 09-navigation-start
```

Gotowe w starcie:
- **nawigacja:** trasy (`Navigation/Routes.cs`), `INavigatedTo<TRoute>`, `NavigationResult`, `RouteMap`,
  `INavigationService` (`NavigateAsync`, `OpenModalAsync`, `CloseModalAsync`, `GoBackAsync`, `ResetToAsync`), w UI
  `NavigationService` (strona i ViewModel z kontenera, przypisanie `BindingContext`, blokada, „najpierw push,
  potem ładowanie”, zakres na modal) i `PageRegistry` (`AddPage`);
- **kreator zakończenia:** `CompleteVisitPage`, `CompleteVisitFlowViewModel`, cztery kroki, `CompletionDraft` (Scoped),
  `CompletionStepTemplateSelector`, pole podpisu `SignaturePad`;
- **testy:** `tests/FieldService.Tests/ContainerTests.cs`, testy kreatora i kroków.

W starcie ktoś „na szybko” dopisał lokalizator usług (`Presentation/Common/ServiceLocator.cs`, ustawiany w
`MauiProgram`) i `VisitDetailsViewModel` bierze z niego `CompleteVisitHandler` – „Zakończ wizytę” kończy bez kreatora.
Lista TODO: `git grep -n "TODO:"`.

## Kroki
1. `src/FieldService.Presentation/Visits/VisitsListViewModel.cs` – TODO `OpenVisitAsync` →
   `navigation.NavigateAsync(new VisitDetailsRoute(new VisitId(item.Id)))`.
2. `src/FieldService.Presentation/Visits/VisitDetailsViewModel.cs` – TODO `CompleteAsync` →
   `navigation.OpenModalAsync(new CompleteVisitRoute(_visitId))`; usuń `ServiceLocator` (klasę i jej ustawienie w
   `MauiProgram`) oraz dyrektywy `#pragma` z TODO.
3. `src/FieldService.Presentation/Visits/VisitsModule.cs` – TODO wpisy `RouteMap` (trzy trasy modułu);
   `src/FieldService.UI/Visits/VisitsPages.cs` – TODO `AddPage<…>()` dla trzech stron.
4. Usuń `[Ignore("Ćwiczenie 09…")]` (`git grep -n "Ćwiczenie 09" tests`) i uruchom `dotnet test`.

## Kryteria akceptacji
- Lista → szczegóły → „Zakończ wizytę” → kreator → zapis → powrót na odświeżoną listę.
- Podwójne szybkie tapnięcie w wizytę otwiera stronę raz.
- `git grep -n ServiceLocator src` i `git grep -n IServiceProvider src/FieldService.Presentation` – pusto.
- `dotnet test` – wszystkie testy zielone (w tym `ContainerTests` i `Presentation_DoesNotUseServiceProvider`).

## Typowe wpadki
- Brak wpisu w `RouteMap` → `NavigationResult.NoRoute` (komunikat w logu); brak `AddPage` – ten sam wynik.
- Strona zarejestrowana bez ViewModelu w module – `ContainerTests` mówi, którego typu brakuje.
- Nawigacja do szczegółów przed zamknięciem modala – strona trafia pod kreator.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 09-navigation
```
