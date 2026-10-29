# Ćwiczenie 08 — przypadek użycia „od testu” z LLM i ADR 0003

**Blok:** 08 · AI w pracy developera: budowa kontekstu i dokumentacja techniczna · dzień 1
· **Start:** gałąź `08-ai-context-start` · **Rozwiązanie:** `08-ai-context` · **Czas:** 13 min · indywidualnie

## User story
Jako technik chcę usunąć z zakresu wizyty omyłkowo dopisaną część, aby protokół i podpis klienta dotyczyły tego, co
naprawdę zamontowałem.

**Reguła R7:** `Visit.RemoveUsedPart(partId, now)` usuwa całą pozycję części z zakresu prac:
- tylko wizyta w trakcie (jak R2) – inaczej `VisitErrors.NotInProgress`;
- część musi być w zakresie – inaczej `VisitErrors.PartNotInScope` (nowy błąd, typ `Validation`);
- jak R3: zmiana zakresu po podpisie unieważnia podpis;
- zdarzenie `PartRemoved`.

## Start
```bash
git fetch && git checkout 08-ai-context-start
```

Gotowe w starcie: `AGENTS.md` (i odsyłacz `.github/copilot-instructions.md`), `src/FieldService.Domain/AGENTS.md`,
`src/FieldService.Presentation/AGENTS.md`, instrukcje `.github/instructions/`, skill
`.github/skills/nowy-przypadek-uzycia/SKILL.md`, dokumentacja `docs/architecture/`, `docs/areas/visits.md`,
`docs/adr/` (bez 0003), `docs/diagrams/`, szablony promptów `docs/ai/prompty/`, listy `docs/ai/review-kodu.md`
i `docs/ai/weryfikacja.md`. W `Visit.cs` i `VisitDetailsViewModel.cs` komentarze `TODO: Ćwiczenie 08 – R7`; testów R7
nie ma – to pierwszy krok.

## Wariant A – z LLM (narzędzie zatwierdzone w Twojej firmie)
1. (1 min) Przeczytaj `AGENTS.md` i `src/FieldService.Domain/AGENTS.md`; otwórz `docs/ai/prompty/01-test-najpierw.md`.
2. (3 min) **Test najpierw:** prompt z regułą R7 → testy `RemoveUsedPart_*` w `tests/FieldService.Domain.Tests/Visits/`
   (co najmniej: usuwa część w trakcie; po podpisie unieważnia podpis; część spoza zakresu → `PartNotInScope`; wizyta
   zakończona → `NotInProgress`). Przejrzyj: czy testy sprawdzają reguły, a nie implementację? `dotnet test` → czerwono.
   Zrób commit testów.
3. (4 min) **Implementacja pod testy:** prompt `02-implementacja-pod-testy.md` – `Visit.RemoveUsedPart`,
   `RemoveUsedPart` + `RemoveUsedPartHandler` według wzoru `CompleteVisit.cs`, „nie zmieniaj testów” – oraz testy
   handlera (`UnknownVisit_ReturnsNotFound`, `Valid_SavesVisit`). `dotnet test` – **wszystkie** projekty.
   Jeśli zdążysz: TODO w `VisitDetailsViewModel.cs` (`RemovePartCommand`) i `VisitDetailsPage.xaml` (przycisk „Usuń”);
   nowy parametr konstruktora dopisz w `SetUp` w `VisitDetailsViewModelTests` – to nie zmiana testów reguły. Po zmianie
   XAML: `dotnet build src/FieldService.UI -c Release -f net10.0-android`.
4. (2 min) **Przegląd:** lista `docs/ai/review-kodu.md`; `git diff --stat HEAD -- tests/` – testy z kroku 2 bez zmian.
5. (3 min) **ADR 0003:** prompt `05-dokumentacja-z-kodu.md` z Twoją kartą z ćwiczenia 06 i `Visit.cs` → szkic
   `docs/adr/0003-wizyta-jako-jednostka-spojnosci.md` według `docs/adr/_szablon.md` (alternatywy: „Zlecenie z wizytami
   w środku”, „bez agregatu, tylko kompensacja”) → sprawdź listą `docs/ai/weryfikacja.md`.

## Wariant B – bez LLM
Te same kroki na gotowych „odpowiedziach LLM”: `docs/ai/przyklady/08-testy.md`, `08-implementacja.md`,
`08-adr-0003.md`. Jest w nich **6 błędów** – znajdź je listami `docs/ai/review-kodu.md` i `docs/ai/weryfikacja.md`,
popraw i dopiero wtedy przenieś kod do repo (krok 3 ręcznie albo z checkpointu).

**Minimum w wariancie B (13 min):** 6 błędów znalezionych i opisanych; poprawione testy i `Visit.RemoveUsedPart`
w repo, `dotnet test` zielony. Handler, ViewModel i ADR 0003 – z checkpointu.

## Kryteria akceptacji (wariant A)
- Testy R7 (co najmniej 4) i handlera (co najmniej 2) zielone; `dotnet test` cały zielony, także `FieldService.Tests`.
- Testy z kroku 2 niezmienione przez implementację.
- `Visit` nadal bez publicznych setterów.
- ADR 0003 ma alternatywę i konsekwencję negatywną, a każda nazwa typu istnieje (`git grep`).

## Typowe wpadki
- LLM „naprawia” test zamiast kodu – dlatego commit testów i `git diff tests/`.
- Jeden prompt na całą funkcję – diff, którego nie da się przejrzeć.
- `using Microsoft.Maui…` w `Presentation` przy dopisywaniu komendy.
- Wklejenie do czatu `appsettings.Local.json` z kluczami.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 08-ai-context
```
