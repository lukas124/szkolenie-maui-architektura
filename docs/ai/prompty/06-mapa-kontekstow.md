# Prompt 06 – aktualizacja mapy kontekstów

**Cel:** po zmianie w kodzie, która dodaje model albo relację (nowy port `I…Gateway`, nowy maper ACL, nowe zdarzenie
publiczne, nowy moduł), dostać propozycję zmiany `docs/architecture/mapa-kontekstow.md`. Propozycję, nie decyzję.

**Kontekst do dołączenia:**
- `docs/architecture/mapa-kontekstow.md` (obecna mapa);
- diff zmiany (`git diff` albo `git diff main...`);
- `src/` (albo co najmniej `Application/Abstractions`, `Infrastructure/*`, `Domain/*/Events`);
- `AGENTS.md`.

## Prompt

```text
Zaktualizuj mapę kontekstów (diagram Mermaid i tabelę relacji) po zmianie z dołączonego diffu.

Zasady:
- NIE DODAWAJ RELACJI, KTÓREJ NIE POTWIERDZA KOD; przy każdej relacji podaj plik w kolumnie „Gdzie w kodzie
  aplikacji” (port, adapter ACL, zdarzenie publiczne albo przestrzeń nazw modułu);
- kierunek strzałki = kierunek logiczny: upstream to model, czyim językiem mówi komunikat – nie ten, kto
  fizycznie wysyła żądanie;
- model bez kodu w aplikacji oznacz „tylko serwer”;
- nie zmieniaj wierszy, których diff nie dotyczy;
- na końcu wypisz listę zmian i dla każdej plik, który ją potwierdza.
```

## Czego oczekiwać

Zmieniony blok Mermaid, nowy albo zmieniony wiersz tabeli i listę „zmiana → plik”.

## Jak przejrzeć

Sekcja „Mapa kontekstów” w [`../weryfikacja.md`](../weryfikacja.md): `git grep` każdej nazwy, istniejące „gdzie
w kodzie”, kierunek logiczny, brak relacji wymyślonych, testy architektury zielone, diagram się renderuje.
