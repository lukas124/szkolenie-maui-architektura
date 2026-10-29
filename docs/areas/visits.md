# Obszar Visits – Realizacja wizyty

Obszar odpowiada na pytanie „co technik zrobił na tej wizycie i czy klient to potwierdził?”. W aplikacji żyje
replika wizyty (SQLite, praca bez zasięgu); źródłem prawdy jest serwer. Pierścienie i przepływ żądania opisuje
[`../architecture/README.md`](../architecture/README.md), znaczenie pojęć – [`../architecture/slownik.md`](../architecture/slownik.md).

## Typy (`src/FieldService.Domain/Visits`)

| Typ | Rola |
|---|---|
| `Visit` | jednostka spójności (agregat, `AggregateRoot<VisitId>`); stan zmienia się tylko przez metody z regułami |
| `VisitId` | identyfikator wizyty |
| `VisitStatus` | `Planned` → `InProgress` → `Completed`; `Planned`/`InProgress` → `Cancelled` |
| `VisitScope` | zakres prac (czynności + zużyte części) – to, co klient podpisuje; zmiana = nowy obiekt |
| `Activity` | wykonana czynność (kod, opis, czas) |
| `UsedPart`, `Quantity` | zużyta część (`PartId` z modelu Magazyn) i ilość większa od zera |
| `CustomerSignature` | kto podpisał, kiedy i pod którą wersją zakresu (`ScopeVersion`) |
| `VisitOutcome`, `VisitOutcomeType` | wynik: `Fixed`, `NeedsFollowUp`, `NotFixed` (dwa ostatnie wymagają powodu) |
| `VisitErrors` | odmowy reguł jako `Error` (kod → tekst `Error_{Code}` w `Strings.resx`) |
| `IVisitRepository` | repozytorium agregatu – interfejs w modelu, implementacja w Infrastructure |
| `Events/VisitEvents.cs` | zdarzenia: `VisitStarted`, `ActivityRecorded`, `PartUsed`, `PartRemoved`, `CustomerSigned`, `CustomerSignatureInvalidated`, `VisitCompleted`, `VisitCancelled` |

Protokół z wizyty (`VisitProtocolBuilder` w `Domain/Protocols`) to osobny model – transformata migawki `VisitScope`.

## Reguły

| Reguła | Treść | Miejsce w kodzie | Błąd |
|---|---|---|---|
| R1 | rozpocząć można tylko wizytę zaplanowaną | `Visit.Start` | `VisitErrors.InvalidTransition` (Conflict) |
| R2 | czynność i część dopisujesz tylko w trakcie wizyty; ilość > 0 | `Visit.RecordActivity`, `Visit.UsePart`, `Quantity.Create` | `VisitErrors.NotInProgress` (Conflict), `VisitErrors.InvalidQuantity` (Validation) |
| R3 | zmiana zakresu po podpisie unieważnia podpis | `Visit.ScopeChanged` (prywatna): `ScopeVersion++`, zdarzenie `CustomerSignatureInvalidated` | – |
| R4 | klient podpisuje niepusty zakres w trakcie wizyty | `Visit.SignByCustomer`, `CustomerSignature.Create` | `VisitErrors.EmptyScope`, `VisitErrors.SignerRequired` (Validation) |
| R5 | zakończyć można wizytę w trakcie; `Fixed` wymaga ważnego podpisu; inne wyniki wymagają powodu | `Visit.Complete`, `VisitOutcome.Create` | `VisitErrors.SignatureRequired`, `VisitErrors.ReasonRequired` (Validation), `VisitErrors.AlreadyCompleted` (Conflict) |
| R6 | zakończonej wizyty nie można anulować | `Visit.Cancel` | `VisitErrors.AlreadyCompleted` (Conflict) |
| R7 | usunąć można tylko część z zakresu i tylko w trakcie wizyty; po podpisie jak R3 | `Visit.RemoveUsedPart`, `VisitScope.WithoutPart` | `VisitErrors.NotInProgress` (Conflict), `VisitErrors.PartNotInScope` (Validation) |

Reguła aplikacyjna (poza agregatem): technik ma w toku najwyżej jedną wizytę – `StartVisitHandler`
(`IVisitRepository.HasOtherInProgressAsync`), błąd `VisitApplicationErrors.AnotherInProgress`; reguła patrzy na kilka
wizyt, a agregat zna tylko siebie. Dane w telefonie należą do jednego technika: logowanie innego technika usuwa dane
poprzedniego (`SignInHandler` + `ILocalTechnicianData`), a gdy czekają niewysłane zmiany – odmawia
(`SessionErrors.UnsentChangesOfOtherTechnician`). Reguła „część musi być w aucie technika” należy do modelu Magazyn
i nie blokuje `UsePart`.

## Przypadki użycia (`src/FieldService.Application/Visits`)

- komendy (`Commands`): `StartVisit`, `RecordActivity`, `UsePart`, `RemoveUsedPart`, `SignByCustomer`,
  `CompleteVisit`, `AddVisitPhoto` – każda z handlerem `…Handler`; wzór: `CompleteVisitHandler`
  (wczytaj → model → zapisz → `Result`);
- zapytania (`Queries`): `GetTodayVisits(OnlyOverdue, Status)` → `TodayVisits` (`VisitSummary` + `OfflineSince` – bez sieci z migawki; filtr i „spóźniona” według
  `OverdueVisitSpecification` z Domain), `GetVisitDetails` → `VisitDetails`, `GetVisitProtocol` → `VisitProtocol`
  (z migawką `CustomerSnapshot`).

## Porty i adaptery

| Port | Pierścień portu | Adapter | Pierścień adaptera |
|---|---|---|---|
| `IVisitRepository` | Domain | `SqliteVisitRepository` (+ `VisitRow`, `VisitRowMapper`) | Infrastructure/Persistence |
| `IVisitReplica` | Application/Abstractions | `SqliteVisitRepository` (ten sam obiekt) | Infrastructure/Persistence |
| `IFlowDraftStore` (szkic kreatora) | Application/Abstractions | `SqliteFlowDraftStore` (tabela `flow_drafts`) | Infrastructure/Persistence |
| `IPreferencesStore` (filtr listy) | Application/Abstractions | `MauiPreferencesStore` | UI/Platform |
| `IVisitsGateway` | Application/Abstractions | `HttpVisitsGateway` + `VisitDtoMapper` (ACL) | Infrastructure/Visits |
| `ISyncScheduler` (wysyłka zmian) | Application/Abstractions | `SyncScheduler` + `OutboxProcessor` (tabela `outbox`) | Infrastructure/Outbox |
| `IOutboxSender` (wiadomość → żądanie) | Infrastructure/Outbox | `HttpVisitsGateway.SendAsync` | Infrastructure/Visits |
| `IVisitPhotoStore` | Application/Abstractions | `SqliteVisitPhotoStore` | Infrastructure/Persistence |
| `ILocalTechnicianData` (dane technika w telefonie) | Application/Abstractions | `SqliteTechnicianData` | Infrastructure/Persistence |
| `IVisitListSnapshotStore` (lista bez sieci) | Application/Abstractions | `SqliteVisitListSnapshotStore` (tabela `visit_list_snapshot`) | Infrastructure/Persistence |
| `IClock` | Application/Abstractions | `SystemClock` | Infrastructure |
| `IInventoryGateway` | Application/Abstractions | `HttpInventoryGateway` | Infrastructure/Inventory |
| `IPermissionService` | Application/Abstractions | `MauiPermissionService` | UI/Platform |
| `IPhotoCapture` | Application/Abstractions | `MauiPhotoCapture` | UI/Platform |

Mapa wiadomość → żądanie (`HttpVisitsGateway.SendAsync`, nagłówki `Idempotency-Key`, `If-Match` i `X-If-Match`):

| Zdarzenie | Żądanie |
|---|---|
| `VisitStarted` | `POST api/visits/{id}/start` |
| `ActivityRecorded` | `POST api/visits/{id}/activities` |
| `PartUsed` | `POST api/visits/{id}/parts` |
| `PartRemoved` | `DELETE api/visits/{id}/parts/{partId}` |
| `CustomerSigned` | `PUT api/visits/{id}/signature` |
| `VisitCompleted` | `POST api/visits/{id}/complete` |

`CustomerSignatureInvalidated` nie idzie na serwer (serwer unieważnia podpis sam po zmianie zakresu), `VisitCancelled`
też nie (anuluje dyspozytor).

Rejestracje: `AddInfrastructure` (`src/FieldService.Infrastructure/DependencyInjection.cs`) i `AddPlatformServices`
(`src/FieldService.UI/DependencyInjection.cs`).

## Prezentacja

`VisitsListViewModel` (lista na dziś, `VisitItemFactory`, `VisitFormatter`, licznik „Oczekuje na wysłanie (N)” i
„Wyślij teraz”, komunikaty `FilterChangedMessage` i `SyncCompletedMessage`), `VisitDetailsViewModel` (Rozpocznij, Dodaj
zdjęcie, Zakończ wizytę → kreator) i kreator zakończenia w `Visits/Completion` (`CompleteVisitFlowViewModel` + kroki
`ActivitiesStepViewModel`, `PartsStepViewModel`, `SignatureStepViewModel`, `SummaryStepViewModel`, stan `CompletionDraft`)
w `src/FieldService.Presentation/Visits`; protokół w ostatnim kroku z zapytania `GetVisitProtocol` (ceny z katalogu
części `IInventoryGateway.GetPartCatalogAsync` – `GET api/parts`). Trasy modułu: `VisitsModule` (`VisitsListRoute`, `VisitDetailsRoute`,
`CompleteVisitRoute`). Strony: `VisitsListPage`, `VisitDetailsPage`, `CompleteVisitPage` w `src/FieldService.UI/Visits/Pages`
(rejestr: `VisitsPages`).

## Testy

| Co | Plik |
|---|---|
| reguły R1–R6 (given–when–then, bez atrap) | `tests/FieldService.Domain.Tests/Visits/VisitTests.cs` |
| reguła R7 (usunięcie części) | `tests/FieldService.Domain.Tests/Visits/VisitRemoveUsedPartTests.cs`, `tests/FieldService.Application.Tests/Visits/RemoveUsedPartHandlerTests.cs` |
| obiekty wartości | `tests/FieldService.Domain.Tests/Visits/QuantityTests.cs`, `ValueObjectTests.cs` |
| protokół | `tests/FieldService.Domain.Tests/Protocols/VisitProtocolBuilderTests.cs` |
| przypadki użycia (z `InMemoryVisitRepository`, `FixedClock`) | `tests/FieldService.Application.Tests/Visits/*HandlerTests.cs` |
| repozytorium SQLite | `tests/FieldService.Infrastructure.Tests/Persistence/SqliteVisitRepositoryTests.cs`, `VisitRepositoryContract.cs` |
| lista bez sieci (migawka) | `tests/FieldService.Application.Tests/Visits/GetTodayVisitsSnapshotTests.cs`, `tests/FieldService.Infrastructure.Tests/Persistence/SqliteVisitListSnapshotStoreTests.cs`, `tests/FieldService.Presentation.Tests/Visits/VisitsListOfflineTests.cs` |
| dane technika w telefonie (zmiana technika) | `tests/FieldService.Application.Tests/Session/SignInHandlerTests.cs`, `tests/FieldService.Infrastructure.Tests/Persistence/SqliteTechnicianDataTests.cs` |
| outbox (zapis w transakcji, wysyłka, kompensacja) | `tests/FieldService.Infrastructure.Tests/Outbox/OutboxTests.cs`, `OutboxMessageTests.cs`, `SyncSchedulerTests.cs` |
| wiadomość → żądanie HTTP | `tests/FieldService.Infrastructure.Tests/Visits/HttpVisitsGatewaySendTests.cs` |
| ViewModele | `tests/FieldService.Presentation.Tests/Visits/*Tests.cs` |
| reguła zależności | `tests/FieldService.Tests/LayerDependencyTests.cs` |

Fałszywki i wizyty w typowych stanach: `tests/FieldService.TestDoubles` (`TestVisits`, `FixedClock`,
`InMemoryVisitRepository`, `FakeVisitPhotoStore`).
