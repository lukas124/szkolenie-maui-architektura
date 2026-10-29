# Ćwiczenie 07 — model wizyty według własnej karty

**Blok:** 07 · Logika biznesowa w kodzie: pierścień domeny i pierścień aplikacji · dzień 1
· **Start:** gałąź `07-agregat-start` · **Rozwiązanie:** `07-agregat` · **Czas:** 28 min · indywidualnie

## User story
Jako kierownik serwisu chcę, aby podpis klienta zawsze dotyczył faktycznie wykonanego zakresu prac, a wizyta
„naprawiona” zawsze miała podpis – także gdy technik pracuje bez zasięgu.

**Wejście:** Twoja karta z ćwiczenia 06 (obiekty wartości, komendy, zdarzenia, reguły).

## Start
```bash
git fetch && git checkout 07-agregat-start
```

Gotowe w starcie:
- **pierścień domeny:**
  - `AggregateRoot<TId>`, `Result`/`Error`/`ErrorType`, `VisitId`, `PartId`;
  - zdarzenia (`Visits/Events`), `VisitErrors`, `VisitScope`, `VisitOutcome`;
  - `Visit` ze stanem, `Start`, `RecordActivity`, `SignByCustomer`, `Cancel`;
  - `VisitProtocolBuilder`, interfejs `IVisitRepository`;
- **pierścień aplikacji:** przypadki użycia (`StartVisit`, `RecordActivity`, `UsePart`, `SignByCustomer`,
  `AddVisitPhoto`, `GetVisitDetails`) i gotowy test `StartVisitHandlerTests` (wzór do kroku 5);
- **infrastruktura:** `SqliteVisitRepository` z `VisitRowMapper`, zdjęcia w `SqliteVisitPhotoStore`;
- **ekran szczegółów wizyty:** Rozpocznij, Dodaj część, Podpis klienta, Zakończ, Dodaj zdjęcie (z obsługą uprawnień).

Lista TODO: `git grep -n "TODO:"`. Obiekty wartości `Quantity` i `CustomerSignature` są w starcie szkieletem:
nazwy, których używa reszta kodu, są gotowe, ale `Create` niczego nie sprawdza – reguły dopisujesz według swojej karty.

## Kroki
1. `src/FieldService.Domain/Visits/Quantity.cs` – TODO `Create(int value)`: ilość większa od zera, inaczej
   `VisitErrors.InvalidQuantity`; wynik `Result<Quantity>`, bez wyjątku.
2. `src/FieldService.Domain/Visits/CustomerSignature.cs` – TODO `Create(signerName, signedAt, scopeVersion)`: pusty
   podpisujący → `VisitErrors.SignerRequired`, imię bez spacji na brzegach. Zwróć uwagę na pole `ScopeVersion` – podpis
   dotyczy konkretnej wersji zakresu.
3. `src/FieldService.Domain/Visits/Visit.cs`:
   - TODO `UsePart(PartId partId, Quantity quantity, DateTimeOffset now)`:
     - tylko wizyta w trakcie (`VisitErrors.NotInProgress`);
     - część trafia do zakresu (`Scope.WithPart`);
     - zdarzenie `PartUsed`;
     - zmiana zakresu unieważnia podpis – gotowa metoda `ScopeChanged(now)`, wzór w `RecordActivity`;
   - TODO `Complete(VisitOutcome outcome, DateTimeOffset now)`:
     - tylko wizyta w trakcie (zakończona → `AlreadyCompleted`, inna → `InvalidTransition`);
     - „naprawione” wymaga ważnego podpisu (`HasValidSignature()`, inaczej `SignatureRequired`);
     - wynik, status i czas zakończenia;
     - zdarzenie `VisitCompleted`.
4. Usuń `[Ignore]` z testów ćwiczenia (`git grep -n "Ćwiczenie 07" tests`) poza `CompleteVisitHandlerTests` i uruchom
   `dotnet test`.
5. (jeśli zdążysz – inaczej z checkpointu) `src/FieldService.Application/Visits/Commands/CompleteVisit.cs` – TODO
   `HandleAsync`: wczytaj → `NotFound` → `visit.Complete(…, clock.Now)` → przy błędzie bez zapisu → zapis → `Result`.
   Usuń `[Ignore]` w `CompleteVisitHandlerTests`.

## Kryteria akceptacji
- `dotnet test` – wszystkie testy zielone.
- Obiekty wartości niemutowalne i porównywane po wartości; `Visit` bez publicznych setterów.
- W aplikacji (z siecią – „Dodaj część” pobiera stan auta technika z serwera): „Zakończ – naprawione” bez podpisu → „Klient musi podpisać aktualny zakres prac.”; po podpisie i
  dopisaniu części napis „Brak ważnego podpisu klienta” wraca; po ponownym podpisie wizyta się kończy.

## Typowe wpadki
- Obiekt wartości jako `class` z setterami albo porównywany po referencji.
- Walidacja w konstruktorze rzucająca wyjątek zamiast `Result`.
- Zdarzenie przed sprawdzeniem reguły.
- `DateTime.Now` w domenie zamiast parametru `now`.
- Zapis w handlerze także przy błędzie – łapie go test `…DoesNotSave`.
- Zwracanie `List<UsedPart>` na zewnątrz agregatu.

## Omówienie
Porównaj swoją kartę z ćwiczenia 06 z checkpointem: czy masz te same obiekty wartości i reguły? Inne nazwy i pola na
karcie są w porządku, jeśli wynikają z reguł – w kodzie nazwy narzuca szkielet startu.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 07-agregat
```
