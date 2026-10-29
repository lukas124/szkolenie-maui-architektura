---
applyTo: "src/FieldService.Domain/**"
---
Ta sama treść co `src/FieldService.Domain/AGENTS.md` (format GitHub Copilot) – zmieniasz jeden plik, zmieniasz oba.

- Projekt bez pakietów i bez referencji do innych projektów FieldService.
- Reguła biznesowa to metoda agregatu (`Visit`) zwracająca `Result` – przewidywalne naruszenie reguły nie rzuca wyjątku.
  Kody błędów w `Visits/VisitErrors.cs` (`Visit.<Nazwa>`, typ `ErrorType`).
- Najpierw sprawdzenie reguły, potem zmiana stanu i zdarzenie (`Raise(new …)`), na końcu `Result.Success()`.
- Zmiana zakresu prac zawsze przez `ScopeChanged(now)` (R3: nowa wersja zakresu, unieważnienie podpisu).
- Bez I/O, HTTP, SQLite i zegara: czas przychodzi parametrem `now`.
- Stan chroniony: prywatne settery, kolekcje jako `IReadOnlyList`, obiekty wartości jako `record` z metodą `Create`.
- Interfejs repozytorium agregatu (`Visits/IVisitRepository.cs`) jest częścią modelu; implementacja – Infrastructure.
- Testy w `tests/FieldService.Domain.Tests` – given–when–then, bez atrap, nazwy `Metoda_Warunek_Wynik`.
