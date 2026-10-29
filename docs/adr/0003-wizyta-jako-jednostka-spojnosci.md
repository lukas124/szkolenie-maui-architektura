# 0003. Wizyta jako jednostka spójności w telefonie

- **Status:** przyjęta
- **Data:** 2026-11-12

## Kontekst
Technik pracuje bez zasięgu i od razu musi wiedzieć, czego brakuje (np. podpisu). Kierownik serwisu wymaga, żeby
podpis klienta zawsze dotyczył faktycznego zakresu prac, a wizyta „naprawiona” zawsze miała ważny podpis – także gdy
brygada pracuje na dwóch telefonach, a dyspozytor zmienia plan. Reguły R1–R7 (zakres, podpis, zakończenie) potrzebują
tych samych danych w tej samej chwili; zdjęcia, notatki i dane klienta żadnej z nich nie potrzebują.

## Rozważane możliwości
1. **Zlecenie z wizytami w środku** – jedna duża jednostka; każda zmiana wizyty blokuje całe zlecenie (konflikty
   brygady i dyspozytora), a telefon musiałby mieć całe zlecenie z historią.
2. **Bez agregatu, tylko kompensacja** – aplikacja zapisuje zmiany bez reguł, serwer odrzuca po synchronizacji; technik
   dowiaduje się o braku podpisu, gdy klienta już nie ma.
3. **Wizyta jako jednostka spójności** – agregat `Visit` z zakresem (`VisitScope`), podpisem (`CustomerSignature`)
   i wynikiem; reguły sprawdzane w telefonie, zapis całości z wersją.

## Decyzja
Jednostką spójności w telefonie jest wizyta: agregat `Visit` (`src/FieldService.Domain/Visits/Visit.cs`) pilnuje reguł
R1–R7, zakres prac i podpis są obiektami wartości wewnątrz, a zapis to jeden wiersz z wersją
(`SqliteVisitRepository`, `UPDATE … WHERE Id = ? AND Version = ?`). Zdjęcia są poza agregatem (`IVisitPhotoStore`),
dane klienta – migawką w modelu odczytu, zlecenie – tylko identyfikatorem (`WorkOrderId`).

## Konsekwencje
- Pozytywne: reguły działają bez sieci i są testowane bez atrap (`tests/FieldService.Domain.Tests/Visits/`); konflikt
  dwóch zapisów tej samej wizyty wykrywa wersja.
- **Negatywne:** te same reguły są też na serwerze (dwie implementacje do zgrania); spójność ze zleceniem i magazynem
  jest tylko ostateczna – zamknięcie zlecenia i rezerwacja części dzieją się później, po synchronizacji, i mogą się
  nie udać (kompensacja).
- Do zrobienia: zmiany wizyty trafiają na serwer przez synchronizację (Blok 11); konflikt z serwerem rozstrzyga
  kompensacja, nie nadpisanie.
