# Architektura FieldService

FieldService to aplikacja mobilna (.NET MAUI) technika serwisu: lista wizyt na dziś, przebieg wizyty (czynności,
zużyte części, podpis klienta, wynik) i praca bez zasięgu na lokalnej replice w SQLite. Ten plik opisuje pierścienie
i drogę żądania przez kod. Język domeny jest w [`slownik.md`](slownik.md), modele i relacje z innymi systemami
w [`mapa-kontekstow.md`](mapa-kontekstow.md), decyzje z uzasadnieniem w [`../adr/`](../adr/).

## Pierścienie

Pięć projektów w `src/`, od środka na zewnątrz:

| Projekt | Co zawiera | Zna |
|---|---|---|
| `FieldService.Domain` | model Realizacji wizyty: `Visit` z regułami, obiekty wartości, `VisitErrors`, zdarzenia, interfejs `IVisitRepository`; `Result`, `Error` | nic |
| `FieldService.Application` | przypadki użycia (`…Handler`), modele odczytu (`VisitSummary`, `VisitDetails`), porty w `Abstractions` (`IVisitsGateway`, `IClock`, …) | Domain |
| `FieldService.Infrastructure` | adaptery portów: API (klient `FieldServiceApiClient` generowany w buildzie z OpenAPI, `ApiExecutor`, bramy `Api…Gateway`, `VisitDtoMapper`), SQLite (`SqliteVisitRepository`), `SystemClock` | Application, Domain |
| `FieldService.Presentation` | ViewModele (CommunityToolkit.Mvvm), modele prezentacji (`VisitItem`, `VisitTone`), porty UI (`IDialogService`, `ILocalizer`, `INavigationService`) | Application, Domain |
| `FieldService.UI` | projekt MAUI: strony XAML, konwertery, adaptery platformy (`MauiPhotoCapture`, `MauiPermissionService`, …), `Strings.resx`, korzeń kompozycji `MauiProgram` | wszystkie |

**Reguła zależności:** zależności w kodzie wskazują tylko do środka. Domain nie zna nikogo, Application zna tylko
Domain, a Infrastructure i Presentation nie znają siebie nawzajem. Port (interfejs) leży w pierścieniu, który go
potrzebuje, a adapter (implementacja) na zewnątrz.

**Tylko UI zna MAUI.** Typy MAUI (`Color`, `ImageSource`, `Application.Current`) są wyłącznie w `FieldService.UI`. ViewModel w `Presentation` dostaje `VisitTone`, a kolor dobiera `ToneToColorConverter` w UI.
Dzięki temu testy ViewModeli działają bez emulatora.

**Korzeń kompozycji:** `MauiProgram.CreateMauiApp` składa pierścienie w kolejności `AddApplication()` →
`AddInfrastructure(api)` → `AddPresentation()` → `AddPlatformServices()` → `AddPages()`. To jedyne miejsce, które zna
wszystkie projekty. Kontener poza nim znają tylko `App`, `NavigationService` (fabryka stron w UI: strona i ViewModel
z kontenera, przypisanie `BindingContext`, zakres na modal) i rozszerzenia napisów w XAML
(`{loc:Translate}`: tworzy je parser XAML, a nie kontener, więc `ILocalizer` biorą z
`IPlatformApplication.Current.Services`) –
ViewModel nigdy (zakaz lokalizatora usług pilnuje test `Presentation_DoesNotUseServiceProvider`).

Reguły pilnują testy architektury w `tests/FieldService.Tests/LayerDependencyTests.cs` (m.in.
`Application_DoesNotDependOnInfrastructure`, `Presentation_DoesNotReferenceMaui`,
`Presentation_DoesNotUseAggregateOrRepository`). Czerwony test oznacza złamaną regułę, a nie zły test.

## Moduły

Moduły odpowiadają modelom z mapy kontekstów i są przestrzeniami nazw w pierścieniach: `Visits`, `Parts`/`Inventory`,
`Orders`, `Protocols`. Każdy moduł prezentacji ma jeden punkt rejestracji (`VisitsModule`, `PartsModule`, `OrdersModule` –
ViewModele i trasy), a w UI swoje strony (`VisitsPages`, `PartsPages`, `OrdersPages`). Kierunek zależności między modułami jest taki jak na mapie
(downstream zna upstream – tylko tam, gdzie relację realizuje kod w telefonie; `VisitCompleted` do Zleceń płynie przez
serwer, więc `Visits` i `Orders` się nie znają) – pilnują go `ModuleBoundaryTests` i `ModuleSharedStateTests`.

## Przepływ żądania

Przykład: technik kończy wizytę jako „naprawioną” (reguła R5).

```text
CompleteVisitPage (UI, XAML – kreator, ostatni krok)
  └─ NextCommand ─▶ CompleteVisitFlowViewModel (Presentation)
        └─ CompleteVisitHandler.HandleAsync(CompleteVisit) (Application)
              ├─ IVisitRepository.GetAsync ──────────▶ SqliteVisitRepository (Infrastructure)
              ├─ Visit.Complete(outcome, clock.Now) (Domain – reguła i zdarzenie VisitCompleted)
              └─ IVisitRepository.SaveAsync ─────────▶ SqliteVisitRepository (Infrastructure)
                                                         ├─ transakcja: wiersz wizyty + wiadomość w outbox
                                                         └─ ISyncScheduler.RequestSync() ─▶ OutboxProcessor
                                                              └─ ApiVisitsGateway.SendAsync ─▶ POST api/visits/{id}/complete
        ◀─ Result (sukces → INavigationService zamyka kreator i wraca na listę; błąd → IUserFeedback.ShowErrorAsync)
```

1. **ViewModel** zbiera intencję użytkownika i wywołuje przypadek użycia. Nie zawiera reguł, nie zna agregatu ani
   repozytorium – dostaje model odczytu `VisitDetails`.
2. **Przypadek użycia** orkiestruje: wczytaj → wywołaj model → zapisz. Czas bierze z `IClock`, reguł aplikacyjnych
   pilnuje sam (np. `StartVisitHandler` sprawdza, czy technik nie ma w toku innej wizyty).
3. **Model domeny** (`Visit`) decyduje: zmienia stan i zgłasza zdarzenie albo zwraca `Result` z błędem
   (`VisitErrors`) bez żadnej zmiany.
4. **Port** (`IVisitRepository`, `IVisitsGateway`, `IVisitReplica`) opisuje, czego przypadek użycia potrzebuje od
   świata zewnętrznego.
5. **Adapter** w Infrastructure albo UI realizuje port: SQLite, HTTP, aparat, uprawnienia.
6. **Wysyłka** idzie osobno: `Result` wraca do ViewModelu po zapisie w telefonie, serwer dostaje zmianę z outbox,
   gdy jest sieć (z kluczem idempotencji i wersją; konflikt → kompensacja).

Błąd wraca jako `Result`, nie jako wyjątek. Tekst dla użytkownika pochodzi z `Strings.resx` (`Error_{Code}`, np.
`Error_Visit.SignatureRequired`) przez `ErrorMessageProvider`.

## Droga żądania HTTP

```text
Api…Gateway (Infrastructure, port z Application)
  └─ ApiExecutor.ExecuteAsync("parts.reserve", …) – wynik i wyjątki → Result
        └─ FieldServiceApiClient (wygenerowany z Api/openapi/fieldservice.v1.json)
              └─ AuthHeaderHandler (token; 401 → jedno odświeżenie i jedno ponowienie)
                    └─ CorrelationIdHandler (X-Correlation-Id)
                          └─ handler odporności (Polly: ponawia tylko odczyty, limity czasu, bezpiecznik)
                                └─ handler systemu (AndroidMessageHandler / NSUrlSessionHandler) ─▶ API
```

Rejestracja: `AddApiClients` w `src/FieldService.Infrastructure/DependencyInjection.cs`. Statusy HTTP → `Error`:
`ApiErrorMapper` (409 z kodem z `ProblemDetails`, np. `Inventory.OutOfStock`). Wygenerowany kod i bramy są `internal` –
DTO nie wychodzą z Infrastructure.

## Cache

| Co | Gdzie | Jak długo |
|---|---|---|
| katalog cen części | `PartsCatalog`: pamięć (TTL 5 min) → API → SQLite `parts_catalog` (bez internetu od razu SQLite) | pamięć: do TTL; SQLite: do wylogowania |
| odpowiedzi GET z ETagiem (JSON: lista, historia, szczegóły, katalog, zlecenia) | `ETagCacheHandler`: SQLite `http_cache`, `If-None-Match` → 304 | do wylogowania |
| lista wizyt | migawka `visit_list_snapshot` (Blok 11) – od razu na ekranie, potem sieć | do wylogowania |
| miniatury | `ThumbnailCache`: pamięć z limitem 50 MB → pliki w `IAppPaths.CacheDirectory` | pamięć: do wyparcia; pliki: do wylogowania |

Wszystko w czystym .NET (Infrastructure); z platformy pochodzi tylko katalog plików cache (`MauiAppPaths`).
`ILocalCaches` czyści wszystko naraz – przy wylogowaniu i zmianie technika.

## Telemetria

Ślad od tapnięcia do bazy: zapis zmiany to span przypadku użycia (`CompleteVisit`, atrybut `visit.id`). Wysyłka z
outboxu to osobny ślad z linkiem do śladu zapisu: `outbox.send` → span wywołania API w `ApiExecutor`
(`visits.send.VisitCompleted`) → żądanie `HttpClient` z nagłówkiem `traceparent` (W3C) → API (`fieldservice-api`,
ASP.NET Core) → zapytanie SQL. Wywołania API poza outboxem (np. `parts.reserve`) to span `ApiExecutor` → `HttpClient` →
API. Miniatury bez śladów (`SuppressInstrumentationScope`). Metryki: `fieldservice.visits.completed`,
`fieldservice.outbox.pending`. Logi: ostrzeżenia i błędy przez OTLP; awarie zapisane przy awarii i wysłane przy
następnym starcie. Eksport tylko z adresem backendu OTLP w konfiguracji (`Telemetry:OtlpEndpoint`).

## Gdzie szukać dalej

- obszar wizyt (typy, reguły R1–R7, porty, testy): [`../areas/visits.md`](../areas/visits.md);
- diagramy C4: [`../diagrams/`](../diagrams/) (`c4-kontekst.puml`, `c4-kontenery.puml`, `obszar-visits.puml`);
- zasady dla LLM i Definition of Done: `AGENTS.md` w korzeniu repozytorium.
