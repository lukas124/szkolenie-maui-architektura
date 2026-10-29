# Ćwiczenie 12 — płynna i czytelna historia wizyt

**Blok:** 12 · Interfejs wspierany przez AI i optymalizacja UI: układy, kontrolki, płynność · dzień 2
· **Start:** gałąź `12-ui-ai-start` · **Rozwiązanie:** `12-ui-ai` · **Czas:** 18 min · indywidualnie

## User story
Jako technik chcę płynnie przewijać kilkaset wizyt w historii i czytelnie widzieć ich status – także w trybie ciemnym
i z dużą czcionką – aby szybko znaleźć poprzednią wizytę u tego samego klienta.

## Start
```bash
git fetch && git checkout 12-ui-ai-start
```

Gotowe w starcie:
- **historia wizyt:** zapytanie `GetVisitHistory` (strony po 30, `GET api/visits/history`), `VisitHistoryViewModel`
  (stronicowanie, `BatchObservableCollection.AddRange`), wiersz `VisitHistoryItem` z `OpenCommand`, przycisk
  „Historia” na liście wizyt;
- **do poprawy:** `src/FieldService.UI/Visits/Pages/VisitHistoryPage.xaml` w wersji „wygenerowanej bez ograniczeń”
  i kontrolka `src/FieldService.UI/Controls/StatusBadge.xaml(.cs)` „od LLM” (jeszcze nieużywana);
- **style:** `ListRow`, `Badge.Neutral` … `Badge.Danger` (`AppThemeBinding`), konwertery `ToneToBadge`, `ToneToColor`,
  `RelativeTime` (`App.xaml`);
- **ściągi:** [`docs/ai/review-xaml.md`](../ai/review-xaml.md) (10 punktów), [`docs/ai/prompt-xaml.md`](../ai/prompt-xaml.md).

Lista TODO: `git grep -n "TODO:"`.

**Krok 0 (w trakcie demo albo przed ćwiczeniem, ~2 min):** zainstaluj start w Release i zmierz „przed” – build Release
trwa dłużej niż Debug:
```bash
dotnet build src/FieldService.UI -c Release -f net10.0-android -t:Install
```
Brak czasu → jako „przed” użyj liczb prowadzącego (pamięć ok. 265 MB PSS na koncie `wydajnosc@szkolenie.pl`).

## Kroki
1. *(4 min) LLM jako recenzent:* daj LLM `VisitHistoryPage.xaml` (+ `.xaml.cs`) i `docs/ai/review-xaml.md`,
   poproś o uwagi według 10 punktów (punkt listy, linia, poprawka). Zaznacz uwagi trafne, fałszywe i to, czego
   recenzent nie zauważył.
2. *(6 min) Popraw stronę* (z LLM albo ręcznie): bez `ScrollView` wokół `CollectionView`; bez
   `XamlCompilation(Skip)`; `x:DataType` na stronie i w `DataTemplate`; jeden `Grid` na wiersz (styl `ListRow`); kolory
   ze stylów (`CardTitle`, `Caption`); `ItemSizingStrategy="MeasureFirstItem"`; komenda wiersza z modelu elementu
   (`Command="{Binding OpenCommand}"`) zamiast `RelativeSource`; miniatura `ThumbnailUrl` zamiast `PhotoUrl`; bez
   `HeightRequest` na tekstach. Napisy weź z zasobów (`{loc:Translate VisitHistory_Title}`, `{loc:Translate VisitHistory_Empty}`
   – klucze są już w `Strings.resx` i `Strings.en.resx`). `RelativeTimeConverter` zostaw – zmierzymy go w Bloku 14.
3. *(4 min) Popraw kontrolkę* `StatusBadge` – trzy błędy od LLM: `BindingContext = this`, `typeof(Color)` zamiast
   `VisitTone` (`Presentation.Visits`), wartość domyślna `0` zamiast `VisitTone.Neutral`. Wewnątrz wiąż przez
   `x:Reference` do korzenia kontrolki (`x:Name="root"`), styl tła z `ToneToBadge`, kolor tekstu z `ToneToColor`. Użyj
   kontrolki w wierszu: `Tone="{Binding Tone}" Text="{Binding StatusText}" SemanticProperties.Description="{Binding StatusText}"`.
4. *(4 min) Weryfikacja:* `dotnet build src/FieldService.UI -c Release -f net10.0-android` bez ostrzeżeń XAML; trzy
   zrzuty (jasny, ciemny, `font_scale 1.3` – polecenia w `docs/ai/review-xaml.md`) do `docs/ai/zrzuty/` obok zrzutów
   „przed”; pomiar przed/po w **Release** na koncie z długą historią (`wydajnosc@szkolenie.pl`, PIN `1234`) –
polecenia w `docs/ai/review-xaml.md`, liczby poniżej.

| Pomiar (Release, otwarcie historii + 15 przewinięć) | Przed | Po |
|---|---|---|
| Pamięć – `meminfo`, TOTAL PSS (MB) | | |
| Janky frames (%) – `gfxinfo` | | |
| 90th percentile (ms) – `gfxinfo` | | |

Bez wirtualizacji (`ScrollView` wokół `CollectionView`) lista trzyma w pamięci więcej wierszy – PSS spada wyraźnie.
Na szybkim komputerze emulator przewija płynnie obie wersje (różnica klatek w szumie pomiaru); klatki rozróżnia słabe
urządzenie albo profil emulatora z małą pamięcią.

**Wariant B (bez dostępu do LLM):** krok 1 na gotowej odpowiedzi „recenzenta”
[`docs/ai/przyklady/12-review-historia.md`](../ai/przyklady/12-review-historia.md) (8 uwag – zaznacz trafne i fałszywe,
dopisz pominiętą); kroki 2–4 ręcznie z listą przeglądu.

## Kryteria akceptacji
- W `VisitHistoryPage.xaml` nie ma `ScrollView` wokół `CollectionView`, jest `x:DataType` na stronie i w `DataTemplate`,
  jeden `Grid` na wiersz, brak wartości „hex”: `git grep -n "#[0-9A-Fa-f]\{6\}" src/FieldService.UI/Visits` – pusto.
- `StatusBadge` bez `BindingContext = this`, `Tone` typu `VisitTone`; status czytelny w trybie ciemnym i przy
  `font_scale 1.3`.
- Pamięć (PSS) „po” < „przed”; `dotnet test` zielony (bez zmian w `Presentation`).

## Typowe wpadki
- `x:DataType` na stronie, ale nie w `DataTemplate` – Release kończy się błędem (bindingi wiersza szukane w ViewModelu
  strony); Debug tego nie pokazuje.
- `MeasureFirstItem` razem z `DataTemplateSelector` – puste wiersze; równe wiersze = jeden szablon (wariant przez
  `DataTrigger`).
- Styl bez `AppThemeBinding` – w trybie ciemnym czarny tekst na czarnym tle.
- Pomiar w Debug; „poprawiony” konwerter (zabiera dowód do ćwiczenia 14), LLM zamienia `CollectionView` na
  `ListView` – przestarzała rada.

## Jeśli utkniesz
```bash
git stash && git fetch && git checkout 12-ui-ai
```
