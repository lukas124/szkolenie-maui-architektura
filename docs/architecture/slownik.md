# Słownik języka FieldService

Wszechobecny język: te same słowa w rozmowie z biznesem, na ekranie, w testach i w kodzie.

| Pojęcie | Znaczenie w FieldService | W kodzie |
|---|---|---|
| Zlecenie | sprawa klienta: urządzenie, adres, usterka, termin SLA; należy do modelu Zlecenia (serwer) | `VisitDetails.WorkOrderNumber` (tylko numer – model na serwerze) |
| Wizyta | jedna obecność technika u klienta w ramach zlecenia; ma przebieg, zakres prac, podpis i wynik | `Visit` (agregat, `VisitId`) |
| Zakres prac | to, co klient podpisuje: czynności i zużyte części; zmienia się w trakcie wizyty | `VisitScope`, `Visit.ScopeVersion` |
| Czynność | wykonana praca (kod czynności i opis) | `Activity`, `Visit.RecordActivity` |
| Zużyta część | część zamontowana u klienta podczas wizyty | `UsedPart`, `Quantity`, `Visit.UsePart` |
| Podpis klienta | akceptacja konkretnego zakresu prac przez klienta | `CustomerSignature`, `Visit.SignByCustomer`, `HasValidSignature()` |
| Wynik wizyty | jak skończyła się wizyta: naprawione, potrzebna kolejna wizyta (np. brak części) albo nie naprawiono | `VisitOutcome`, `VisitOutcomeType`, `Visit.Complete` |
| Protokół | dokument z wizyty wyliczany z zakresu prac – klient widzi go przed podpisem | `VisitProtocol`, `VisitProtocolBuilder` |
| Zapas części | stan jednej części w jednym magazynie i jej rezerwacje (model Magazyn, serwer) | – (serwer) |
| Rezerwacja | odłożenie sztuk części dla wizyty; wygasa po czasie | – (serwer) |
| Auto technika | części, które technik ma przy sobie | `GetVanStock`, `VanStockItem` |
| Dyspozytor | przyjmuje zgłoszenie klienta, zakłada zlecenie, planuje wizyty, może je anulować albo przenieść | – (poza aplikacją; `Visit.Cancel` odtwarza jego decyzję) |
| Brygada | dwóch techników na jednej wizycie, każdy z telefonem | `Visit.TechnicianIds` (kilku techników na wizycie) |
