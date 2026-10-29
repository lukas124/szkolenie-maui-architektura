# Prompt 03 – refaktoryzacja ViewModelu pod SRP

**Cel:** dostać **plan** podziału zbyt dużego ViewModelu według zasady jednej odpowiedzialności (ang. Single
Responsibility Principle), zanim powstanie jakikolwiek kod.

**Kontekst do dołączenia:**
- `AGENTS.md`, `src/FieldService.Presentation/AGENTS.md`;
- ViewModel do podziału (np. `VisitsViewModel` z gałęzi `01-typowe-mvvm`);
- wzór po podziale: `src/FieldService.Presentation/Visits/VisitsListViewModel.cs`, `IVisitFormatter.cs`,
  `IVisitItemFactory.cs`, `src/FieldService.Presentation/Feedback/IUserFeedback.cs`.

## Prompt

```text
Zaproponuj plan podziału <NazwaViewModelu> według SRP. NAJPIERW PLAN, BEZ KODU.

Dla każdej odpowiedzialności podaj: co robi, do jakiego typu trafia, w którym projekcie (Domain,
Application, Infrastructure, Presentation, UI) i dlaczego tam – zgodnie z regułą zależności z AGENTS.md.
Porty do świata zewnętrznego (HTTP, SQLite) są w Application/Abstractions, adaptery w Infrastructure;
ViewModel nie zna HttpClient ani typów MAUI. Wskaż, które typy już istnieją w repozytorium
(<lista plików>), a które trzeba dodać.
Na końcu: lista kroków, każdy mały, z testem.
```

## Czego oczekiwać

Tabela „odpowiedzialność → typ → projekt” i lista kroków. Dobry plan przypomina wynik Bloków 02–03: przypadek użycia
z portem `IVisitsGateway`, `IVisitFormatter`, `IVisitItemFactory`, `IUserFeedback`.

## Jak przejrzeć

- Czy żaden port nie trafia do `Presentation` (np. „serwis z HTTP” obok ViewModelu)?
- Czy nazwy istniejących typów się zgadzają (`git grep`)?
- Czy kroki da się przejrzeć osobno (jeden krok = jeden commit)?
- Kod dopiero po akceptacji planu – kolejnym promptem, krok po kroku.
