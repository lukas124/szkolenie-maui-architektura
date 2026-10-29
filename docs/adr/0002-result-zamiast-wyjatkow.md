# 0002. `Result` z typem błędu zamiast wyjątków dla przewidywalnych błędów

- **Status:** przyjęta
- **Data:** 2026-11-12

## Kontekst
Technik często trafia na sytuacje przewidywalne: brak sieci, odmowa reguły („klient musi podpisać zakres”), konflikt
wersji z drugim telefonem brygady, odmowa uprawnienia do aparatu. W `01-typowe-mvvm` były to wyjątki łapane w
ViewModelach, a komunikat składano z `ex.Message`. Nieobsłużony wyjątek z komendy zamyka aplikację na Androidzie.

## Rozważane możliwości
1. Wyjątki domenowe (`SignatureRequiredException`) – znany mechanizm, ale niewidoczny w sygnaturze; łatwo zapomnieć
   `catch` i trudno odróżnić błąd programisty od odmowy reguły.
2. `bool` + `out string error` – proste, ale bez typu błędu, więc UI nie wie, jak zareagować.
3. `Result` / `Result<T>` z `Error(Code, ErrorType)` – wynik w sygnaturze, typ decyduje o reakcji UI.

## Decyzja
Metody modelu, przypadki użycia i adaptery zwracają `Result` albo `Result<T>` (`src/FieldService.Domain/Common/Result.cs`).
`Error` ma kod (`Visit.SignatureRequired`) i `ErrorType` (Validation, NotFound, Conflict, Unauthorized, Network,
Unexpected). Tekst dla człowieka wybiera prezentacja po kodzie (`ErrorMessageProvider`, `Strings.resx`). Wyjątek zostaje
dla błędu programisty.

## Konsekwencje
- Pozytywne: odmowa widoczna w sygnaturze i w testach (`Assert.That(result.Error, Is.EqualTo(VisitErrors.…))`); jeden
  sposób na komunikaty; reakcja UI zależna od typu (np. konflikt → świeże dane).
- **Negatywne:** więcej kodu przy każdym wywołaniu (`if (!result.IsSuccess) return …`); wyjątki z bibliotek (HTTP, JSON)
  trzeba zamieniać na `Result` w adapterach – zapomniany przypadek dalej kończy się wyjątkiem.
- Do zrobienia: każdy nowy kod błędu dostaje wpis `Error_<Kod>` w `Strings.resx`.
