# Ćwiczenie 13 — rezerwacja części przez wygenerowany klient i `ApiExecutor`

**Blok:** 13 · Komunikacja ze światem: REST API i Result Pattern · dzień 2
· **Start:** gałąź `13-api-start` · **Rozwiązanie:** `13-api` · **Czas:** 15 min · indywidualnie

## User story
Jako technik chcę zarezerwować brakującą część z magazynu dla wizyty i od razu wiedzieć, czy się udało, a gdy ktoś
był szybszy – dostać zrozumiały komunikat, aby nie jechać do magazynu na próżno.

## Start
```bash
git fetch && git checkout 13-api-start
dotnet build
```

Gotowe w starcie (czytasz, nie piszesz):
- **klient generowany w buildzie:** `<OpenApiReference>` w `src/FieldService.Infrastructure/FieldService.Infrastructure.csproj`
  ze specyfikacji `Api/openapi/fieldservice.v1.json` → `FieldServiceApiClient` (F12 na nazwie – plik w `obj/`);
- **`ApiExecutor`** (`Infrastructure/Api`) – odpowiedź i wyjątki (także Polly) → `Result`; **`ApiErrorMapper`** – status
  HTTP + pole `code` z `ProblemDetails` → `Error`;
- **potok `HttpClient`:** `AddApiClients` w `Infrastructure/DependencyInjection.cs` – `AuthHeaderHandler` (token,
  odświeżenie przy 401), `CorrelationIdHandler`, `AddStandardResilienceHandler(ConfigureResilience)`;
- **rezerwacja poza bramą:** `ReservePartHandler` (Application), `ReservePartViewModel` i strona „Rezerwacja z magazynu”
  (przycisk „Brakuje? Zarezerwuj z magazynu” w kroku „Części” kreatora zakończenia), komunikaty w `Strings.resx`.

Lista TODO: `git grep -n "TODO:"`.

## Kroki
1. *(6 min)* `ApiInventoryGateway.ReserveAsync` (`src/FieldService.Infrastructure/Inventory`): `ReservePartRequest`
   z komendy → `executor.ExecuteAsync("parts.reserve", c => client.ReservePartAsync(request, c), ct)` → błąd zwróć dalej,
   sukces zamień na `PartReservation`. Wzór – metody wyżej w tym samym pliku. Bez `try/catch`.
2. *(3 min)* `ApiErrorMapper.Map`: 409 → `ErrorType.Conflict` z kodem z treści odpowiedzi (`Inventory.OutOfStock`),
   bez kodu – `Api.Conflict`. Ta sama gałąź obsługuje konflikt wersji wizyty z outboxu (`Visit.VersionConflict`, Blok 11).
3. *(2 min)* `ReservePartViewModel.ReserveAsync`: błąd → `feedback.ShowErrorAsync(result.Error!)`.
4. *(4 min)* Usuń `[Ignore("Ćwiczenie 13: …")]` (`git grep -n "Ćwiczenie 13"`), `dotnet test`. Na emulatorze: wizyta
   w toku → „Zakończ wizytę” → krok „Części” → „Brakuje? Zarezerwuj z magazynu” → część **Sprężarka rotacyjna R32**,
   „Zarezerwuj”. Ostatnią sztukę w magazynie centralnym zarezerwowało inne konto brygady – API zawsze odpowiada
   409 `Inventory.OutOfStock`. Inna część → „Zarezerwowano … do …”.

## Kryteria akceptacji
- `dotnet test` zielony, bez `[Ignore]` z „Ćwiczenie 13”.
- Rezerwacja sprężarki → komunikat „Ostatnią sztukę zarezerwował już ktoś inny – sprawdź auto albo inny magazyn.”
  (`Error_Inventory.OutOfStock` z zasobów, nie ogólne „Nieoczekiwany błąd…”).
- W `ApiInventoryGateway` nie ma `try/catch`, DTO (`ReservePartRequest`, `ReservationDto`) nie wychodzą z
  Infrastructure.

## Typowe wpadki
- Edycja pliku w `obj/` – znika przy następnym buildzie; zmiana kontraktu = specyfikacja (`tools/update-openapi.sh`).
- `try/catch` w bramie zamiast `ApiExecutor`; brak `ct` przekazanego do klienta (`c => …(request, c)`).
- DTO zwrócone do Application zamiast `PartReservation`.
- 409 zawsze jako `Inventory.OutOfStock` – kod bierzemy z odpowiedzi; konflikt wersji wizyty to inny kod.
- 404 na wszystko → `BaseAddress` bez `/` na końcu albo ścieżka od `/`.
- Wyjątek walidacji opcji „SamplingDuration…” przy pierwszym żądaniu → `SamplingDuration` < 2 × `AttemptTimeout`.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 13-api
```
