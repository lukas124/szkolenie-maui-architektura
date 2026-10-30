# FieldService – instrukcje dla LLM

Aplikacja technika serwisu w terenie (.NET 10 MAUI, Android i iOS): wizyty na dziś, przebieg wizyty, części, podpis
klienta i zakończenie – także bez zasięgu. Reguły wizyty działają w telefonie, serwer jest źródłem prawdy.

## Projekty i reguła zależności
- `FieldService.Domain` – model (agregat `Visit`, obiekty wartości, `Result`), zero pakietów.
- `FieldService.Application` – przypadki użycia (`…Handler`), porty do świata zewnętrznego w `Abstractions/`
  (`I…Gateway`, `IClock`, `IVisitReplica`), modele odczytu. Interfejs repozytorium agregatu (`IVisitRepository`) jest
  częścią modelu – leży w Domain.
- `FieldService.Infrastructure` – adaptery: API (klient generowany z OpenAPI, `ApiExecutor`, bramy i mapery ACL),
  SQLite (repozytorium, Data Mapper).
- `FieldService.Presentation` – ViewModele, formatter, fabryki, fasada komunikatów `IUserFeedback`; porty prezentacji
  (`IDialogService`, `ILocalizer`, `INavigationService`) z adapterami w UI.
- `FieldService.UI` – jedyny projekt MAUI: strony XAML, adaptery platformy, korzeń kompozycji (`MauiProgram`).
- Zależności (strzałka = „zna”): `UI` → `Presentation` → `Application` → `Domain` oraz
  `UI` → `Infrastructure` → `Application`. **Tylko UI zna MAUI.** Pilnują tego testy w `tests/FieldService.Tests`.

## Moduły i stan
- Moduł = model z mapy kontekstów (`docs/architecture/mapa-kontekstow.md`), przestrzeń nazw w każdym pierścieniu:
  `Visits` (Realizacja wizyty), `Parts`/`Inventory` (Magazyn części), `Orders` (Zlecenia), `Protocols` (Protokoły).
- Zależność między modułami w aplikacji tylko tam, gdzie relację z mapy realizuje kod w telefonie: `Visits` może używać
  publicznych typów `Parts` (Magazyn jest upstreamem: auto technika, katalog cen). `Parts` i `Orders` nie znają `Visits`,
  a `Visits` nie zna `Orders` (zdarzenie `VisitCompleted` do Zleceń płynie przez serwer). Wspólne porty, stan i komunikaty
  leżą poza modułami. Pilnują `tests/FieldService.Tests/ModuleBoundaryTests.cs` i `ModuleSharedStateTests.cs`.
- Stan aplikacji (dane wspólne, dłuższe niż ekran) w `IAppState` (singleton w kontenerze); o zmianie ViewModel dowiaduje
  się z komunikatu `WeakReferenceMessenger`, nie z zdarzenia singletona. Stan, który musi przeżyć ubicie procesu, zapisujemy
  od razu po zmianie (filtr – `IPreferencesStore`, szkic kreatora – `IFlowDraftStore`).

## Dane offline
- Zapis najpierw w telefonie: `SqliteVisitRepository.SaveAsync` w jednej transakcji zapisuje wiersz wizyty i wiadomość
  w tabeli `outbox` dla każdego zdarzenia (`OutboxMessage.From`) – transakcyjna skrzynka nadawcza. Nie wysyłamy z
  ViewModelu ani z handlera.
- Wysyła `OutboxProcessor` (`src/FieldService.Infrastructure/Outbox`): w kolejności zapisu, w obrębie wizyty po kolei;
  `Idempotency-Key` = `OutboxMessage.Id` (nadany przy zapisie), wersja z kolumny `ServerVersion` w `If-Match` i
  `X-If-Match` (proxy hostingu wycina `If-Match`). `Network`/`Unauthorized` (i limit żądań 429) → stop,
  `Visit.VersionConflict` → kompensacja (serwer wygrywa: wiadomości wizyty `Failed`, lokalna kopia usunięta, komunikat
  `SyncErrors.Conflict`), inny błąd → ponowienie w następnym przebiegu, po 5 próbach cała wizyta jak przy konflikcie
  (`SyncErrors.Rejected`).
- Outbox i `ISyncScheduler` leżą poza przestrzenią `Visits`, ale niosą tylko zmiany wizyt – `Orders` i `Parts` ich nie
  używają (`ModuleSharedStateTests`).
- Kiedy wysyłać decyduje `SyncScheduler` (port `ISyncScheduler`): zapis, logowanie, powrót z tła i odzyskanie sieci
  (`UI/Platform/SyncTriggers`), „Wyślij teraz”. Nie obiecujemy wysyłki w tle – lista pokazuje „Oczekuje na wysłanie (N)”.
- „Lokalne zmiany” wizyty = jej oczekujące wiadomości w `outbox`; takiej wizyty dane z serwera nie nadpisują.
- Lista bez sieci: każda pobrana lista trafia do migawki (`IVisitListSnapshotStore`, tabela `visit_list_snapshot`);
  przy `ErrorType.Network` `GetTodayVisits` zwraca migawkę (`TodayVisits.OfflineSince`), lista pokazuje
  „Bez sieci – dane z …”.
- Dane w telefonie należą do jednego technika: logowanie innego usuwa je (`ILocalTechnicianData.ClearAsync`), a przy
  oczekujących wiadomościach poprzedniego technika logowanie odmawia – jego zmiany nie mogą pójść z cudzym tokenem.

## Komunikacja z API
- Klienta `FieldServiceApiClient` generuje NSwag przy każdym `dotnet build` ze specyfikacji
  `src/FieldService.Infrastructure/Api/openapi/fieldservice.v1.json` (wynik w `obj/` – nie edytujemy, nie commitujemy).
  Nowa wersja kontraktu: `tools/update-openapi.sh`, potem `git diff`. Po zmianie `Options` w `<OpenApiReference>` usuń
  `src/FieldService.Infrastructure/obj/fieldservice.v1Client.cs` – generator nie zauważa zmiany samych opcji.
- Każde wywołanie przez `ApiExecutor.ExecuteAsync("obszar.operacja", c => client.…Async(…, c), ct)`: bramy nie mają
  `try/catch`, statusy HTTP → `Error` tylko w `ApiErrorMapper`. Wygenerowany kod i bramy `Api…Gateway` są `internal`;
  port w Application, DTO mapowane w bramie (ACL).
- Potok `HttpClient` (`AddApiClients`): `AuthHeaderHandler` (token) → `CorrelationIdHandler` → handler odporności
  (Polly: automatyczne ponowienia tylko odczytów, limit próby 20 s i całości 60 s, bezpiecznik). Zapisów nie ponawia
  Polly – wizyty ponawia outbox z `Idempotency-Key`, rezerwację technik.
- Sesja: 401 → `TokenRefresher` (jedno odświeżenie naraz) i jedno ponowienie żądania; nieudane odświeżenie →
  `ISessionExpiry` → ekran logowania, outbox zostaje. Refresh token tylko w `ISessionStore` (`SecureStorage`).

## Cache i wydajność
- Cache to czysty .NET w Infrastructure, za portami z Application: `IPartsCatalog` → `PartsCatalog` (pamięć z TTL →
  API → tabela SQLite `parts_catalog`; bez internetu – `INetworkStatus` – od razu SQLite, bez HTTP i ponowień),
  `ETagCacheHandler` w potoku `HttpClient` (GET z `If-None-Match`, 304 → zapamiętana treść z tabeli `http_cache`),
  `IThumbnailCache` → `ThumbnailCache` (pamięć z limitem rozmiaru → plik w katalogu cache z `IAppPaths.CacheDirectory`
  → sieć). Cache bez limitu to wyciek z ładną nazwą.
- Lista wizyt: stale-while-revalidate w `VisitsListViewModel` – pusta lista → `GetTodayVisits(FromSnapshot: true)`
  (migawka z Bloku 11, „Dane z 7:42 – odświeżanie…”), potem lista z sieci.
- Czyszczenie: `ILocalCaches.ClearAsync` przy wylogowaniu (Ustawienia → „Wyloguj”, `SignOutHandler`) i zmianie technika
  (`SqliteTechnicianData`); wizyty i niewysłane zmiany zostają.
- Tekst elementu listy liczy fabryka raz (`VisitItemFactory`, `IVisitFormatter.FormatRelativeDate`), nie konwerter
  przy każdym wiązaniu. Subskrypcje w ViewModelu: `WeakReferenceMessenger` + `UnregisterAll` w `OnClosed()`;
  test wycieku – `tests/FieldService.Presentation.Tests/LeakTests.cs` (`WeakReference`, pełny GC).
- Konfiguracje: Debug (`.dev`, Diagnostyka), **Test** (`.test`: optymalizacje jak Release – przycinanie, profilowane AOT –
  plus Diagnostyka; pomiary), Release (sklep). Stała `DIAGNOSTICS` w Debug i Test: strona Diagnostyka (Ustawienia),
  `RequestCounterHandler`, log `http.requests` i `visits.list.shown`. Mierzymy w Test, nie w Debug.

## Telemetria i raporty awarii
- OpenTelemetry: Application zna tylko `Diagnostics/AppTelemetry` (`ActivitySource` i `Meter` „FieldService” z .NET – bez
  pakietów) i port `ICrashReporter`; SDK, instrumentacja `HttpClient` i eksporter OTLP/HTTP tylko w
  `Infrastructure/Telemetry` (`AddTelemetry(options)`), rejestracja i start w korzeniu kompozycji (`MauiProgram`).
- Span: jeden na wywołanie API w `ApiExecutor` (nazwa = operacja, przy błędzie `error.type`), przypadki użycia o
  znaczeniu biznesowym (`CompleteVisit`), wysyłka z outboxu (`outbox.send` z linkiem do śladu zapisu). Atrybuty: `visit.id`
  tak; nazwisko klienta, podpis, e-mail, token – nigdy.
- Awaria nieobsłużona → `PendingCrashStore` (plik w chwili awarii, `UI/Telemetry/CrashHandlers`), wysyłka przy następnym
  starcie jako log OTLP; wyjątek obsłużony (`FireAndForget`, nieobserwowany `Task`) → `ICrashReporter.RecordHandled`.
- Konfiguracja jak w aplikacji z podstaw: `appsettings.json` + osadzony `appsettings.{Konfiguracja}.json` (środowisko,
  telemetria) + opcjonalny `appsettings.Local.json` poza repo (lokalne API i backend OTLP, `.gitignore`).

## Konwencje
- Nazwy z języka domeny (`docs/architecture/slownik.md`): `Visit`, `VisitScope`, `CustomerSignature`.
- Komenda `CompleteVisit` + `CompleteVisitHandler`; zapytanie `GetVisitDetails` → model odczytu `VisitDetails`;
  ViewModel `…ViewModel`; port bramy `I…Gateway`; testy `Metoda_Warunek_Wynik`.
- Komentarze po polsku, mówią, co kod robi. Kod najprostszy, który spełnia testy.

## Wzorce – tak u nas robimy
- Reguła biznesowa: metoda agregatu zwraca `Result`, zdarzenie po sprawdzeniu reguły – `src/FieldService.Domain/Visits/Visit.cs`.
- Przypadek użycia: wczytaj → model → zapisz → `Result` – `src/FieldService.Application/Visits/Commands/CompleteVisit.cs`.
- Zapis z wiadomością do wysłania w jednej transakcji – `src/FieldService.Infrastructure/Persistence/SqliteVisitRepository.cs`.
- ViewModel koordynuje, nie formatuje i nie zna stron – `src/FieldService.Presentation/Visits/VisitsListViewModel.cs`.
- Test agregatu given–when–then bez atrap – `tests/FieldService.Domain.Tests/Visits/VisitTests.cs`;
  test handlera z `InMemoryVisitRepository` i `FixedClock` – `tests/FieldService.Application.Tests/Visits/CompleteVisitHandlerTests.cs`.
- Szczegóły warstwy: `src/FieldService.Domain/AGENTS.md`, `src/FieldService.Presentation/AGENTS.md`,
  `src/FieldService.UI/AGENTS.md` (XAML); obszar Visits:
  `docs/areas/visits.md`; decyzje: `docs/adr/`.

## Polecenia
- `dotnet build FieldService.slnx` – zero ostrzeżeń (`TreatWarningsAsErrors`).
- Po zmianie XAML: `dotnet build src/FieldService.UI -c Release -f net10.0-android` – zły binding wychodzi dopiero w Release.
- `dotnet test FieldService.slnx` – wszystkie projekty testowe, także testy architektury.

## Definition of Done
1. Nowa reguła ma testy (najpierw test, potem kod).
2. `dotnet test` – wszystkie testy zielone, w tym `FieldService.Tests`.
3. Zero ostrzeżeń kompilatora (po zmianie XAML także w buildzie Release).
4. Teksty w `src/FieldService.UI/Resources/Strings/Strings.resx` i `Strings.en.resx`, XAML bierze je przez `{loc:Translate Klucz}`
   (wzorzec w `StringFormat` – `{loc:TranslateFormat Klucz}`).
5. Brak `TODO:` w zmienionych plikach.
6. Decyzja architektoniczna = nowy ADR w `docs/adr/` (szablon `_szablon.md`).

## Zakazy
- Lokalizator usług (`ServiceHelper`, `IServiceProvider` w ViewModelu) – ukrywa zależności; wszystko przez konstruktor.
  Jedyny wyjątek: rozszerzenia napisów w XAML (`MarkupExtensions/Translate*Extension.cs` w UI) biorą `ILocalizer`
  z `IPlatformApplication.Current.Services`, bo rozszerzenie tworzy parser XAML, a nie kontener.
- Typy MAUI w ViewModelu (`Color`, `ImageSource`, `Application.Current`) – ViewModel testujemy bez emulatora.
- `DateTime.Now`, `DateTimeOffset.Now`, `UtcNow` w Domain i Application – czas daje `IClock` albo parametr `now`.
- `async void` poza `FireAndForget` – wyjątek ginie albo zamyka aplikację.
- Zdarzenie singletona albo statyczne zdarzenie subskrybowane w ViewModelu bez odpięcia – smycz: ViewModel nie zostanie zebrany.
- `MessagingCenter`, `StrongReferenceMessenger` i `appState.Filter.PropertyChanged +=` w ViewModelu – przestarzałe API albo wycieki pamięci.
- DTO poza Infrastructure, agregat w ViewModelu – granice pierścieni; ViewModel dostaje model odczytu.
- `try/catch` wokół wywołania API w bramie i edycja wygenerowanego klienta – od tego jest `ApiExecutor` i specyfikacja.
- Zmiana testów, żeby kod przeszedł – test jest specyfikacją; najpierw zapytaj.
