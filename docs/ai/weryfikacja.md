# Weryfikacja dokumentacji z LLM

Dokument wygenerowany z kodu (ADR, README, diagram C4, mapa kontekstów) to szkic. Zanim trafi do repozytorium,
przejdź poniższe punkty. Dokument, który wymienia nieistniejący typ, jest gorszy niż brak dokumentu.

## Każdy dokument

1. **Każda nazwa istnieje.** Dla każdego typu, pliku i przestrzeni nazw z dokumentu: `git grep -n "<Nazwa>" -- src tests`
   (szukaj w kodzie, nie w `docs/`).
   Brak trafień = halucynacja – usuń albo zastąp prawdziwą nazwą.
2. **Strzałki zgodne z regułą zależności.** Na diagramie zależności wskazują do środka (UI → Presentation →
   Application → Domain; Infrastructure → Application); żadnej strzałki Presentation → Infrastructure ani
   Application → Infrastructure. Te same reguły sprawdzają testy w `tests/FieldService.Tests/LayerDependencyTests.cs`.
3. **Diagram się renderuje.** `.puml` – podgląd PlantUML w IDE albo `plantuml -checkonly docs/diagrams/*.puml`;
   Mermaid – podgląd Markdown w VS Code albo na GitHubie.
4. **ADR ma alternatywę i konsekwencję negatywną.** Co najmniej jedna rozważana i odrzucona możliwość oraz co najmniej
   jedna konsekwencja negatywna; status „proponowana”, dopóki zespół nie przyjmie decyzji.
5. **Brak sekretów i danych osobowych.** Żadnych kluczy, tokenów, haseł, adresów produkcyjnych
   (lokalne pliki konfiguracji z kluczami), prawdziwych imion, telefonów i adresów klientów, podpisów.

```bash
git grep -n "<Nazwa>" -- src tests     # 0 trafień w kodzie = nazwa wymyślona
dotnet test tests/FieldService.Tests    # reguła zależności w kodzie
```

## Mapa kontekstów

Dotyczy `docs/architecture/mapa-kontekstow.md` po aktualizacji z LLM
(prompt [`prompty/06-mapa-kontekstow.md`](prompty/06-mapa-kontekstow.md)).

1. **Każda nazwa istnieje.** Każdy typ i przestrzeń nazw z mapy (diagram i tabela) daje trafienie w `git grep`.
2. **Moduł z kodem ma przestrzeń nazw.** Każdy moduł, który ma kod w aplikacji, ma swoją przestrzeń nazw w pierścieniach
   – kolumna „Kod w aplikacji” w tabeli modułów (`Visits` = Realizacja wizyty, `Protocols` = Protokoły,
   `Parts`/`Inventory` = Magazyn części; kolejne dochodzą z kodem). Moduł bez kodu w aplikacji jest oznaczony
   „tylko serwer”.
3. **Każda relacja ma „gdzie w kodzie”.** Kolumna wskazuje istniejący port, adapter ACL, zdarzenie publiczne albo
   moduł. Relacji bez potwierdzenia w kodzie nie dopisujemy (np. Shared Kernel, którego nic w kodzie nie pokazuje).
4. **Kierunek strzałki = kierunek logiczny.** Upstream to moduł, czyim językiem mówi komunikat. Aplikacja, która
   wysyła komendę do cudzego kontraktu (np. „wyślij powiadomienie”), jest downstreamem, choć fizycznie wysyła.
5. **Kierunek zależności w kodzie bez zmian.** Adapter (ACL) w Infrastructure, port w Application; testy
   architektury zielone (`dotnet test tests/FieldService.Tests`).
6. **Diagram się renderuje.** Blok Mermaid otwiera się w podglądzie Markdown bez błędu.
