# Przegląd kodu z LLM

Plik instrukcji (`AGENTS.md`) to prośba, testy to gwarancja, a ta lista to przegląd człowieka między nimi. Przejdź
ją przy każdym diffie od LLM, zanim zrobisz commit. Jeden punkt „nie” = poprawka przed commitem.

1. **Nazwy z języka domeny.** Typy, metody i testy używają słów ze [słownika](../architecture/slownik.md)
   (`Visit`, `VisitScope`, `UsedPart`, `RemoveUsedPart`), a nie ogólników (`Manager`, `Service`, `Helper`, `Data`).
2. **`Result` zamiast wyjątków.** Odmowa reguły to `Result` z błędem z `VisitErrors` (albo
   `VisitApplicationErrors`); brak `throw` dla przewidywalnych sytuacji.
3. **Czas z `IClock`.** Brak `DateTime.Now`, `DateTimeOffset.Now` i `UtcNow` w Domain, Application i testach;
   model dostaje `now` jako parametr, handler bierze `clock.Now`, test – stałą wartość albo `FixedClock`.
4. **Zdarzenie po regule.** `Raise(...)` dopiero po wszystkich sprawdzeniach i zmianie stanu; odmowa nie zostawia
   zdarzenia ani zmiany (a zmiana zakresu przechodzi przez R3 – unieważnienie podpisu).
5. **Brak zmian w istniejących testach.** Testy przejrzane w kroku „test najpierw” są nietknięte; LLM nie
   „naprawił” testu zamiast kodu.
6. **Brak nowych pakietów i referencji.** Żaden `*.csproj` ani `Directory.Packages.props` się nie zmienił; adapter
   nie jest tworzony w przypadku użycia (`new Sqlite…`, `new HttpClient`).
7. **Brak typów MAUI poza UI.** Żadnego `using Microsoft.Maui…`, `Color`, `ImageSource`, `Application.Current`
   w `Presentation`, `Application`, `Domain`, `Infrastructure`.
8. **Teksty w zasobach.** Komunikat dla użytkownika to klucz w `src/FieldService.UI/Resources/Strings/Strings.resx`
   (`Error_{Code}` dla nowego błędu), nie napis w kodzie.

## Polecenia

```bash
git diff --stat tests/                               # punkt 5: tylko nowe pliki/testy
git diff --stat -- '*.csproj' Directory.Packages.props  # punkt 6: pusto
git grep -nE "DateTime(Offset)?\.(Now|UtcNow)" -- src/FieldService.Domain src/FieldService.Application tests  # punkt 3
git grep -n "Microsoft.Maui" -- src/FieldService.Presentation src/FieldService.Application src/FieldService.Domain src/FieldService.Infrastructure  # punkt 7
dotnet build FieldService.slnx                       # zero ostrzeżeń (TreatWarningsAsErrors)
dotnet test                                          # WSZYSTKIE projekty, w tym FieldService.Tests (architektura)
```
