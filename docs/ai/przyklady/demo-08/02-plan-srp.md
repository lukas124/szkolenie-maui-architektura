Nagrana odpowiedź LLM do dema (plan B) – do weryfikacji, nie wzorzec.

# Demo 08 · krok 2 – plan podziału `VisitsViewModel` według SRP

Kontekst: `VisitsViewModel` z gałęzi `01-typowe-mvvm`, `src/FieldService.Presentation/AGENTS.md`, prompt
`docs/ai/prompty/03-refaktoryzacja-srp.md`.

## Odpowiedź LLM (pierwsza)

| Odpowiedzialność w `VisitsViewModel` | Nowy typ | Projekt |
|---|---|---|
| tekst statusu, okna czasowego, „spóźniona” | `IVisitFormatter` + `VisitFormatter` | Presentation |
| budowa elementów listy z danych wizyty | `IVisitItemFactory` + `VisitItemFactory` | Presentation |
| komunikaty błędów i okna dialogowe | `IUserFeedback` + `UserFeedback` | Presentation |
| pobranie wizyt z API (`HttpClient`, token) i zapis w SQLite | `IVisitService` + `VisitService` (HTTP + SQLite) | Presentation |
| stan ekranu (`Visits`, `IsLoading`, `IsOffline`) i komenda odświeżenia | `VisitsListViewModel` | Presentation |

Kroki: 1) formatter z testem, 2) fabryka z testem, 3) `IUserFeedback`, 4) `VisitService`, 5) ViewModel tylko
koordynuje.

## Uwaga przy przeglądzie

`AGENTS.md`: porty do świata zewnętrznego są w `Application/Abstractions`, adaptery w Infrastructure, a `Presentation`
nie zna `HttpClient` ani SQLite. „Serwis z HTTP” obok ViewModelu łamie regułę zależności (test
`Presentation_DoesNotDependOnInfrastructure` i zakaz z `AGENTS.md`).

## Odpowiedź LLM po wskazaniu zakazu

| Odpowiedzialność | Typ | Projekt |
|---|---|---|
| tekst statusu, okna czasowego | `IVisitFormatter` + `VisitFormatter` | Presentation |
| elementy listy | `IVisitItemFactory` + `VisitItemFactory` | Presentation |
| komunikaty i dialogi | `IUserFeedback` + `UserFeedback` | Presentation |
| wizyty na dziś (przypadek użycia) | `GetTodayVisits` + `GetTodayVisitsHandler` → `VisitSummary` | Application |
| port do API wizyt | `IVisitsGateway` | Application/Abstractions |
| adapter HTTP i mapowanie DTO | `HttpVisitsGateway` + `VisitDtoMapper` | Infrastructure |
| stan ekranu i koordynacja | `VisitsListViewModel` | Presentation |

Ten plan pokrywa się z wynikiem Bloków 02–03 (`src/FieldService.Presentation/Visits`,
`src/FieldService.Application/Visits/Queries`).
