# Prompt 01 – test najpierw

**Cel:** dostać od LLM testy nowej reguły modelu, zanim powstanie kod. Test jest specyfikacją, którą
przeglądasz szybciej niż implementację (test najpierw, ang. Test-Driven Development – K. Beck).

**Kontekst do dołączenia:**
- `AGENTS.md`, `src/FieldService.Domain/AGENTS.md`;
- `docs/areas/visits.md` (reguły R1–R7);
- `src/FieldService.Domain/Visits/Visit.cs`, `VisitErrors.cs`, `Events/VisitEvents.cs`;
- `tests/FieldService.Domain.Tests/Visits/VisitTests.cs` (wzór nazw i pomocników `Given…`, `AssertFailedWithoutChange`).

## Prompt

```text
Napisz WYŁĄCZNIE testy NUnit dla nowej reguły modelu, bez implementacji.

Reguła <Rn>: <treść reguły jednym zdaniem, np. „RemoveUsedPart(partId, now) usuwa część z zakresu
tylko w InProgress; część spoza zakresu → VisitErrors.PartNotInScope; po podpisie zmiana zakresu
unieważnia podpis (jak R3)”>.

Metoda: <Visit.Metoda(parametry)>. Zwraca Result, nie rzuca wyjątków.
Przypadki (co najmniej):
1. <sukces w poprawnym stanie – stan po zmianie i zdarzenie>;
2. <po podpisie – podpis unieważniony, ScopeVersion + 1, CustomerSignatureInvalidated>;
3. <błąd walidacji – kod błędu i brak zmiany stanu>;
4. <zły status – VisitErrors.NotInProgress i brak zmiany stanu>.

Zasady:
- dopisz testy do tests/FieldService.Domain.Tests/Visits/VisitTests.cs, nazwy Metoda_Warunek_Wynik;
- given–when–then, użyj istniejących pomocników Given…, Capture, AssertFailedWithoutChange;
- czas tylko ze stałej Now z klasy testów (nigdy DateTime.Now);
- używaj tylko typów, które istnieją w plikach z kontekstu; jeśli czegoś brakuje (np. kod błędu),
  wypisz to osobno jako „do dodania”, nie wymyślaj.
```

## Czego oczekiwać

Kilka testów `<Metoda>_<Warunek>_<Wynik>`, które się nie kompilują albo są czerwone (metody jeszcze nie ma) i listę
brakujących nazw (np. nowy kod w `VisitErrors`).

## Jak przejrzeć

- Czy każdy test sprawdza regułę z karty, a nie szczegół implementacji (np. prywatne pole)?
- Czy przypadek odmowy sprawdza kod błędu **i** brak zmiany stanu?
- Czy oczekiwany wynik zgadza się z regułą (np. wizyta `Completed` to odmowa, nie sukces)?
- Czy czas jest stały (`Now`), a zdarzenia porównywane z konkretnymi wartościami?
- `dotnet test tests/FieldService.Domain.Tests` – czerwono z właściwego powodu. Zatwierdź testy osobnym commitem.
