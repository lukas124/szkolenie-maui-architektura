# Ćwiczenie 11 — outbox dla zdarzeń wizyty

**Blok:** 11 · Praca z danymi: SQLite i tryb offline · dzień 2
· **Start:** gałąź `11-offline-start` · **Rozwiązanie:** `11-offline` · **Czas:** 20 min · indywidualnie

## User story
Jako technik pracujący bez zasięgu chcę kończyć wizyty offline, a aplikacja ma je wysłać sama po powrocie sieci – nawet
po zamknięciu przez system – i jasno powiedzieć, gdy dyspozytor w międzyczasie coś zmienił.

## Start
```bash
git fetch && git checkout 11-offline-start
```

Gotowe w starcie:
- **tabela `outbox`** (migracja 3) i `OutboxMessage.From(zdarzenie)` – `Id` = klucz idempotencji, `Type`, `Payload`;
- **wysyłka jednej wiadomości:** `HttpVisitsGateway.SendAsync` (mapa wiadomość → żądanie, `Idempotency-Key`,
  `If-Match` i `X-If-Match` z wersją z kolumny `ServerVersion`);
- **kiedy wysyłać:** `SyncScheduler` (port `ISyncScheduler`), wyzwalacze w `UI/Platform/SyncTriggers` (powrót z tła,
  odzyskanie sieci), po zalogowaniu (`StartupNavigator`), „Wyślij teraz” i licznik „Oczekuje na wysłanie (N)” na liście;
- **w `OutboxProcessor`:** zapytanie o oczekujące wiadomości, `MarkSentAsync`, `MarkAttemptFailedAsync`, `CompensateAsync`.

Lista TODO: `git grep -n "TODO:"`.

## Kroki
1. `src/FieldService.Infrastructure/Persistence/SqliteVisitRepository.cs` – TODO w `SaveAsync`: w
   `connection.RunInTransactionAsync(c => …)` (lambda **synchroniczna**) `UPDATE` wiersza wizyty z kontrolą wersji, a
   gdy się udał – `INSERT INTO outbox` dla każdej wiadomości z `visit.DomainEvents` (`OutboxMessage.From`, bez `null`).
   Po transakcji: `IncrementVersion()`, `ClearDomainEvents()`, `sync.RequestSync()`.
2. `src/FieldService.Infrastructure/Outbox/OutboxProcessor.cs` – TODO w `ProcessAsync`: pętla po oczekujących;
   `expectedVersion` z `visits.ServerVersion`; sukces → `MarkSentAsync`; `Network`/`Unauthorized` → `break`;
   `Visit.VersionConflict` → `CompensateAsync` + wizyta do `conflicts`; inny błąd → `MarkAttemptFailedAsync`, a gdy to
   była ostatnia próba (`MaxAttempts`) – także `CompensateAsync` + wizyta do `rejected`. Po błędzie kolejne wiadomości
   **tej samej wizyty** pomijasz (`stopped`), innych wizyt – wysyłasz.
3. Usuń `[Ignore("Ćwiczenie 11…")]` (`git grep -n "Ćwiczenie 11" tests`) i uruchom `dotnet test`.

## Kryteria akceptacji
- `dotnet test` – wszystkie testy zielone, w tym `OutboxTests`.
- Tryb samolotowy → „Rozpocznij wizytę” → na liście „Oczekuje na wysłanie (1)”; wyłączenie trybu samolotowego →
  licznik znika, wizyta „W trakcie” także na serwerze.
- Po `adb shell am force-stop pl.szkolenie.fieldservice.dev` w trybie samolotowym i ponownym uruchomieniu z siecią
  (zalogowanie) zmiany docierają na serwer.

## Podgląd kolejki w telefonie
```bash
adb exec-out run-as pl.szkolenie.fieldservice.dev cat files/fieldservice-replica.db3 > /tmp/fs.db3
sqlite3 /tmp/fs.db3 "select Type, Attempts, LastError, SentAt, Failed from outbox order by CreatedAt;"
```

## Typowe wpadki
- `ClearDomainEvents()` przed zapisem – do `outbox` nie trafia nic.
- `async` lambda w `RunInTransactionAsync` – transakcja kończy się przed zapisem wiadomości.
- `break` przy każdym błędzie – jedna zła wiadomość blokuje kolejkę na zawsze; `continue` bez `stopped` – wiadomości
  jednej wizyty idą w złej kolejności.
- Nowy `Guid` przy każdej wysyłce – klucz idempotencji nadaje się raz, przy zapisie.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 11-offline
```
