# FieldService.Presentation – instrukcje dla LLM

Doprecyzowuje `AGENTS.md` z korzenia dla plików tego projektu.

- `net10.0` bez MAUI: żadnych `Color`, `ImageSource`, `Application.Current`, `Shell`, `DisplayAlert`. Wygląd wybiera UI
  (np. `VisitTone` → `ToneToColorConverter`).
- ViewModel koordynuje: przypadek użycia → `Result` → model prezentacji albo `IUserFeedback.ShowErrorAsync(error)`.
  Nie formatuje (to `IVisitFormatter`), nie buduje elementów listy (to `IVisitItemFactory`), nie składa tekstów.
- Nawigacja ViewModel-first przez `INavigationService` (`NavigateToAsync<TViewModel>(parameter)`); ViewModel nie zna stron.
- Dane ładujemy w hakach `BaseViewModel` (`OnNavigatedToAsync`, `OnAppearingAsync`), komendy `[RelayCommand]` z `Task`.
- ViewModel dostaje model odczytu (`VisitDetails`, `VisitSummary`), nigdy agregat ani repozytorium.
- Teksty przez klucze zasobów (`ILocalizer`, `IUserFeedback`) – wartości w `src/FieldService.UI/Resources/Strings/Strings.resx`.
- Testy w `tests/FieldService.Presentation.Tests` – bez emulatora: prawdziwe przypadki użycia, porty Application jako
  atrapy (Moq) albo fałszywki (`InMemoryVisitRepository`, `FixedClock`), porty prezentacji jako atrapy.
