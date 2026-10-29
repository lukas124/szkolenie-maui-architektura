# Ćwiczenie 02 — testy architektury i naprawa trzech naruszeń

**Blok:** 02 · Architektura aplikacji: Czysta Architektura w praktyce mobilnej · dzień 1
· **Start:** gałąź `02-clean-architecture-start` · **Rozwiązanie:** `02-clean-architecture` · **Czas:** 19 min

## User story
Jako lider zespołu chcę, aby CI odrzucał kod, w którym ViewModele zależą od MAUI, a przypadki użycia od szczegółów
API, aby architektura nie rozjechała się po kolejnych sprintach.

## Start
```bash
git fetch && git checkout 02-clean-architecture-start
```

Gotowe w starcie: kod z `01-typowe-mvvm` rozłożony na pięć projektów –
- `VisitsListViewModel` w `Presentation`;
- strony w `UI`;
- `GetTodayVisits`, `GetTodayVisitsHandler` i `VisitSummary` w `Application`;
- brama HTTP (`HttpVisitsGateway`, `VisitDto`, `VisitDtoMapper`) – na razie w `Application/Visits` (to jedno z
  naruszeń, krok 5);
- `MauiProgram` z wywołaniami `Add…()`.

W kodzie są **trzy celowe naruszenia** reguły zależności. Lista TODO: `git grep -n "TODO:"`.

## Kroki
1. `tests/FieldService.Tests/LayerDependencyTests.cs` – usuń `[Ignore]` i uzupełnij TODO w testach
   `Presentation_DoesNotReferenceMaui` oraz `Application_DoesNotDependOnInfrastructure` (zakaz: `FieldService.Infrastructure`
   i `System.Net.Http`). Uruchom `dotnet test`.
   Testy powinny być **czerwone** – to dobrze, właśnie złapały naruszenia.
2. `src/FieldService.Presentation/Visits/VisitItem.cs` – zamień `Color StatusColor` na `VisitTone Tone`. W
   `VisitsListViewModel` przypisz ton ze statusu:
   - spóźniona → `Danger`;
   - w trakcie → `Info`;
   - zakończona → `Success`;
   - anulowana → `Warning`;
   - zaplanowana → `Neutral`.
3. `src/FieldService.UI/Converters/ToneToColorConverter.cs` (gotowy szkielet) – uzupełnij mapowanie na kolory z
   `Resources/Styles/Colors.xaml`. W `Visits/Pages/VisitsListPage.xaml` i `Visits/Pages/VisitDetailsPage.xaml` zmień
   binding koloru statusu na `{Binding Tone, Converter={StaticResource ToneToColor}}`.
4. Przenieś `VisitTemplateSelector.cs` z `src/FieldService.Presentation/Visits` do `src/FieldService.UI/Selectors`
   (zmień przestrzeń nazw i `xmlns` w `VisitsListPage.xaml`). Z `FieldService.Presentation.csproj` usuń
   `<UseMaui>true</UseMaui>` i pakiet `Microsoft.Maui.Controls` – od teraz kompilator nie pozwoli na typy MAUI.
5. Brama HTTP do API leży w projekcie `Application` (`src/FieldService.Application/Visits/HttpVisitsGateway.cs`,
   `VisitDto.cs`, `VisitDtoMapper.cs`), a `GetTodayVisitsHandler` sam ją tworzy. Napraw to:
   - przenieś trzy pliki do `src/FieldService.Infrastructure/Visits` (przestrzeń nazw
     `FieldService.Infrastructure.Visits`); `HttpVisitsGateway` implementuje port `IVisitsGateway`;
   - `GetTodayVisitsHandler` przyjmuje `IVisitsGateway` i `IClock` w konstruktorze;
   - w `src/FieldService.Infrastructure/DependencyInjection.cs` (TODO) zarejestruj bramę:
     `services.AddHttpClient<IVisitsGateway, HttpVisitsGateway>(http => Configure(http, options));`.

## Kryteria akceptacji
- `dotnet test` – wszystkie testy zielone.
- Lista wizyt wygląda jak przed zmianą (te same kolory statusów).
- Polecenie zwraca pusty wynik:
  ```bash
  git grep -n "Microsoft.Maui" src/FieldService.Presentation src/FieldService.Application src/FieldService.Domain src/FieldService.Infrastructure
  ```

## Typowe wpadki
- Test zielony od razu – reguła sprawdza złą przestrzeń nazw albo zły zestaw (assembly).
- Konwerter zwraca zawsze szary – nie ma przypadku dla któregoś tonu albo zły klucz koloru (`Colors.xaml`).
- Zapomniana rejestracja portu – `Unable to resolve service for type IVisitsGateway`.
- Stary binding w XAML (np. `StatusColor` w `VisitDetailsPage.xaml`) – build Debug go nie wyłapie; sprawdź
  `dotnet build src/FieldService.UI -c Release -f net10.0-android`.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 02-clean-architecture
```
