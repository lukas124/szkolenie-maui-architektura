# FieldService.Presentation – instrukcje dla LLM

Doprecyzowuje `AGENTS.md` z korzenia dla plików tego projektu.

- `net10.0` bez MAUI: żadnych `Color`, `ImageSource`, `Application.Current`, `Shell`, `DisplayAlert`. Wygląd wybiera UI
  (np. `VisitTone` → `ToneToColorConverter`).
- ViewModel koordynuje: przypadek użycia → `Result` → model prezentacji albo `IUserFeedback.ShowErrorAsync(error)`.
  Nie formatuje (to `IVisitFormatter`), nie buduje elementów listy (to `IVisitItemFactory`), nie składa tekstów.
- Nawigacja ViewModel-first przez `INavigationService` i trasy (`NavigateAsync(new VisitDetailsRoute(id))`,
  `OpenModalAsync(...)`); ViewModel nie zna stron ani kontenera. Nowa trasa: rekord w `Navigation/Routes.cs` + wpis
  w module (`VisitsModule` → `RouteMap`) + strona w UI (`AddPage<TPage, TViewModel>()`).
- Parametr trasy przez `INavigatedTo<TRoute>`, powrót na ekran przez `OnAppearingAsync`, sprzątanie w `OnClosed()`;
  komendy `[RelayCommand]` z `Task`.
- Przepływ kilku kroków (kreator): jedna strona + ViewModele kroków (`CompletionStep`) + stan przepływu Scoped
  (`CompletionDraft`) – bez `if (step is …)`.
- ViewModel dostaje model odczytu (`VisitDetails`, `VisitSummary`), nigdy agregat ani repozytorium.
- Wynik pracy w tle (wysyłka zmian) przychodzi komunikatem (`SyncCompletedMessage`, wysyła UI na wątku UI) – ViewModel
  nie subskrybuje zdarzeń singletona.
- Teksty przez klucze zasobów (`ILocalizer`, `IUserFeedback`) – wartości w `src/FieldService.UI/Resources/Strings/Strings.resx`.
- Testy w `tests/FieldService.Presentation.Tests` – bez emulatora: prawdziwe przypadki użycia, porty Application jako
  atrapy (Moq) albo fałszywki (`InMemoryVisitRepository`, `FixedClock`), porty prezentacji jako atrapy.
