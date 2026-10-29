# Prompt 05 – dokumentacja z kodu (ADR, README, C4)

**Cel:** dostać szkic dokumentu na podstawie kodu: ADR (ang. Architecture Decision Record, M. Nygard), README obszaru
albo diagram C4 (S. Brown). Szkic, nie decyzję – decyzję podejmuje zespół.

**Kontekst do dołączenia:**
- `AGENTS.md`;
- dla ADR: `docs/adr/_szablon.md`, istniejące ADR w `docs/adr/`, materiał wejściowy (np. karta z ćwiczenia 06),
  pliki kodu, których dotyczy decyzja (np. `src/FieldService.Domain/Visits/Visit.cs`);
- dla README obszaru: pliki obszaru w `src/` i `tests/`, wzór `docs/areas/visits.md`;
- dla C4: pliki `*.csproj`, `MauiProgram.cs`, `DependencyInjection.cs` z każdego projektu, wzór `docs/diagrams/*.puml`.

## Prompt

```text
Na podstawie dołączonych plików napisz <ADR NNNN „tytuł” wg docs/adr/_szablon.md | README obszaru <nazwa> |
diagram C4 <poziom> w C4-PlantUML (!include <C4/C4_...>)>.

Zasady:
- każda nazwa typu, pliku i przestrzeni nazw musi występować w dołączonych plikach; nie wymyślaj typów;
  jeśli czegoś nie wiesz, napisz „do uzupełnienia”;
- ADR: kontekst (fakty) → rozważane możliwości (co najmniej: <alternatywa 1>, <alternatywa 2>) → decyzja →
  konsekwencje, w tym CO NAJMNIEJ JEDNA NEGATYWNA → status „proponowana”;
- C4: strzałki zależności zgodne z regułą zależności z AGENTS.md;
- bez sekretów, adresów produkcyjnych i danych osobowych.
```

## Czego oczekiwać

Plik Markdown albo `.puml` gotowy do przeglądu, z miejscami „do uzupełnienia”, jeśli LLM czegoś nie wiedział.

## Jak przejrzeć

Lista [`../weryfikacja.md`](../weryfikacja.md): `git grep` każdej nazwy, kierunek strzałek, renderowanie, alternatywa
i konsekwencja negatywna w ADR, brak sekretów.
