# Ćwiczenie 03 — refaktoryzacja listy wizyt

**Blok:** 03 · Warstwa prezentacji: MVVM z CommunityToolkit.Mvvm i SRP w ViewModelach · dzień 1
· **Start:** gałąź `03-presentation-start` · **Rozwiązanie:** `03-presentation` · **Czas:** 15 min

## User story
Jako programista chcę, aby ViewModel listy wizyt tylko zarządzał stanem ekranu i koordynował usługi, aby zmiana
formatu godzin albo komunikatu błędu nie wymagała zmian w ViewModelu i testu na emulatorze.

## Start
```bash
git fetch && git checkout 03-presentation-start
```

Gotowe w starcie:
- **szkielet aplikacji:**
  - `BasePage` z nagłówkiem (`Templates/PageTemplate.xaml`);
  - `DialogPage` + `MauiDialogService`;
  - `NavigationService` – jeden `NavigationPage` w `App.CreateWindow`, stronę i ViewModel tworzy usługa
    nawigacji i to ona przypisuje `BindingContext`;
- **interfejsy:** `IVisitFormatter`, `IVisitItemFactory`, `IUserFeedback`, `IErrorMessageProvider`, `INavigationService`;
- **klasy:** `BaseViewModel` (haki cyklu życia), `UserFeedback`, `ErrorMessageProvider`, model prezentacji `VisitItem`;
- **teksty:** w `Strings.resx` i `Strings.en.resx`, XAML bierze je przez `{loc:Translate Klucz}`
  (`MarkupExtensions/TranslateExtension.cs`), wzorzec w `StringFormat` przez `{loc:TranslateFormat Klucz}`.

Lista TODO: `git grep -n "TODO:"`. Do wglądu – „gruby” ViewModel z poprzedniego bloku:
`git show 02-clean-architecture:src/FieldService.Presentation/Visits/VisitsListViewModel.cs`.

## Kroki
1. `src/FieldService.Presentation/Visits/VisitFormatter.cs` – TODO `FormatTimeWindow(from, to)`. Wynik zależy od dnia:
   - dziś → „dziś 10:00–11:30”;
   - jutro → „jutro 8:00–9:30”;
   - inny dzień → „sob. 14.11 14:00–15:30”.

   Kultura i strefa czasowa telefonu pochodzą z `ILocalizer` (`CurrentCulture`, `TimeZone`), „teraz” z `IClock.Now`,
   a wzorce godziny i dnia z zasobów (`Format_Time`, `Format_Day` w `Strings.resx` i `Strings.en.resx`).
2. `src/FieldService.Presentation/Visits/VisitItemFactory.cs` – TODO `Create(VisitSummary)`: `VisitItem` z tekstami z
   formattera, tekstem statusu z `ILocalizer` i tonem ze statusu (jak w `02-clean-architecture`).
3. `src/FieldService.Presentation/Visits/VisitsListViewModel.cs` – konstruktor już przyjmuje potrzebne usługi:
   - TODO w `LoadAsync` (wołane z `OnNavigatedToAsync` i przy odświeżaniu): przypadek użycia → `Result` →
     `Visits.ReplaceWith(... items.Create)`, podsumowanie
     z formattera, a przy błędzie `feedback.ShowErrorAsync(error)`;
   - TODO `[RelayCommand] OpenVisitAsync(VisitItem item)` → `navigation.NavigateToAsync<VisitDetailsViewModel>(item)`;
     w `Visits/Pages/VisitsListPage.xaml` podepnij `OpenVisitCommand` w `TapGestureRecognizer`;
   - usuń `OnVisitTapped` z `VisitsListPage.xaml.cs` – w code-behind zostaje tylko konstruktor.
4. `tests/FieldService.Presentation.Tests/Visits/` – usuń `[Ignore]` w `VisitFormatterTests`,
   `VisitFormatterRegionTests`, `VisitItemFactoryTests` i `VisitsListViewModelTests`.

## Kryteria akceptacji
- Lista pokazuje te same wizyty i kolory statusów (okno czasowe z „dziś/jutro”); dotknięcie wizyty otwiera
  szczegóły, strzałka w nagłówku wraca.
- W ViewModelu nie ma `ToString(`, `new VisitItem`, składania tekstów ani `async void`:
  ```bash
  git grep -nE 'ToString\(|new VisitItem|async void|localizer\[' src/FieldService.Presentation/Visits/VisitsListViewModel.cs
  ```
- W code-behind strony tylko konstruktor.
- `dotnet test` – wszystkie testy zielone.

## Typowe wpadki
- Formatter z `DateTime.Now` zamiast `IClock` – test „dziś/jutro” zależy od dnia uruchomienia.
- `Visits = new ObservableCollection<VisitItem>(…)` zamiast `Visits.ReplaceWith(…)` – widok trzyma starą kolekcję
  i lista się nie odświeża.
- `new VisitItemFactory(...)` w ViewModelu zamiast usługi z konstruktora.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 03-presentation
```
