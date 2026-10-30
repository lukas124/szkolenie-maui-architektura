# Ćwiczenie 14 — ślad, gorący punkt i wyciek pamięci

**Blok:** 14 · Wydajność: profilowanie i wycieki pamięci (analiza z AI), cache · dzień 2
· **Start:** gałąź `14-performance-start` · **Rozwiązanie:** `14-performance` · **Czas:** A – 10 min (+ 2 min na
checkpoincie), B – 14 min · indywidualnie

Dwa ćwiczenia: **A** – ślad przewijania historii wizyt i gorący punkt naszego kodu, **B** – wyciek pamięci w kreatorze
zakończenia wizyty. Narzędzie wybierasz sam: **.NET Meteor 6.2.9** (VS Code) albo **wiersz poleceń** (`dotnet-trace`,
`dotnet-gcdump`, `dotnet-dsrouter` 9.0.661903). Oba działają na macOS i Windows; ćwiczymy na emulatorze Androida,
symulator iOS (tylko macOS, wiersz poleceń) jest opcją.

## Start

```bash
git fetch && git checkout 14-performance-start
dotnet build
```

Gotowe w starcie:
- **konfiguracja Test** (`-c Test`): optymalizacje jak Release + Diagnostyka, osobna aplikacja „FieldService TEST”;
- **Ustawienia** (ikona w nagłówku listy wizyt) → **Diagnostyka**: licznik żądań HTTP, wpisy cache, **żywe ViewModele**
  (szczegóły wizyty, kreator) i „Wymuś GC” (Debug i Test);
- `.vscode/launch.json`: „Meteor: Profiler (trace)” i „Meteor: GC Dump”;
- `tools/gcdiff` – porównanie dwóch zrzutów sterty (które typy przybyły), `tools/retpath` – ścieżka retencji (kto
  trzyma obiekt); oba uruchamiasz `dotnet run --project tools/… -- …` z katalogu repo;
- testy wycieku `tests/FieldService.Presentation.Tests/LeakTests.cs` z `[Ignore]`; dwa testy „Demo 14” (szczegóły
  wizyty) zostają pominięte – ich poprawkę pokazuje prowadzący.

Przygotowanie wiersza poleceń (raz): `dotnet tool install -g dotnet-trace --version 9.0.661903` (tak samo
`dotnet-gcdump` i `dotnet-dsrouter`); `dotnet-trace --version` → `9.0.661903…`. Linia 10.x z Androidem na Mono kończy
się `EndOfStreamException`.

---

## Ćwiczenie A – ślad i gorący punkt

**User story:** Jako programista FieldService chcę wskazać pomiarem, która metoda naszego kodu kosztuje najwięcej przy
przewijaniu historii wizyt, aby poprawiać to, co naprawdę spowalnia listę, a nie to, co tylko wygląda na wolne.

Bez zmian w kodzie. Konto `wydajnosc@szkolenie.pl` (2000 wizyt w historii), PIN `1234`.

### Ślad – Meteor (VS Code)
1. Paleta poleceń: „.NET Meteor: Select workspace project” → `FieldService.UI`; „Select project configuration” →
   **Test**; „Select device” → emulator.
2. Run and Debug → „Meteor: Profiler (trace)” → **Ctrl+F5**. Pierwszy build trwa minutę albo dłużej.
3. Logowanie `wydajnosc@szkolenie.pl` → **Historia** → ok. 20 szybkich przewinięć w dół (ok. 10 s).
4. **Stop (Shift+F5)** – nie zamykaj aplikacji (inaczej stosy bez nazw metod). W Debug Console: „Conversion complete”.
   Plik: `src/FieldService.UI/pl.szkolenie.fieldservice.test.speedscope.json`.
5. Prawy klik na pliku → „Open With…” → **Speedscope**.

### Ślad – wiersz poleceń (Android)
1. Terminal 1 (zostaw otwarty): `dotnet-dsrouter server-server -tcps 127.0.0.1:9100` → zanotuj `pid=…`.
2. Terminal 2, katalog repo:
   ```bash
   dotnet build src/FieldService.UI -f net10.0-android -c Test -t:Install -p:AndroidEnableProfiler=true -p:DiagnosticAddress=10.0.2.2 -p:DiagnosticPort=9100 -p:DiagnosticSuspend=false -p:DiagnosticListenMode=connect
   ```
3. Uruchom „FieldService TEST” z ikony → logowanie `wydajnosc@szkolenie.pl` → **Historia**.
4. `dotnet-trace collect -p <pid routera> --duration 00:00:00:12 --format speedscope -o historia.nettrace` i od razu ok.
   20 szybkich przewinięć → `historia.speedscope.json` w bieżącym katalogu.
5. https://www.speedscope.app → Browse → `historia.speedscope.json` (plik nie opuszcza przeglądarki) albo Speedscope w
   VS Code.

### Analiza w speedscope (oba narzędzia)
1. **Wątek UI**: lista wątków u góry (`t`). Wątek UI to ten, w którym wyszukiwanie `OnBindViewHolder` coś znajduje
   (w śladzie z Meteora zwykle 1., z `dotnet-trace` zwykle ostatni).
2. **Sandwich** (klawisz `3`) → szukaj (Cmd+F / Ctrl+F): `!FieldService.UI.` – wykrzyknik oddziela nazwę modułu od
   metody, więc zostają tylko metody naszego projektu UI (samo `FieldService` łapie też metody MAUI z naszymi typami w
   nawiasach ostrych). Kliknij nagłówek **Total** (malejąco).
3. Pomiń start, logowanie i nawigację (`MainActivity.OnCreate`, `App.CreateWindow`, `CreateMauiApp`, `NavigationService`,
   `LoginPage`, `BasePage`) – ślad z Meteora obejmuje całą sesję. Szukasz pierwszej metody, która w górnym wykresie
   (**Callers** – kto woła) ma nad sobą `n_OnBindViewHolder` (wiązanie wiersza listy). Kod wygenerowany z XAML
   (`…InitializeComponent…LoadDataTemplate`, `StatusBadge..ctor`) to tworzenie widoku wiersza – zanotuj, ale szukasz
   metody napisanej przez nas.
4. Zapisz: nazwę metody, **Total** (All Instances), Total `n_OnBindViewHolder` (wyszukaj go) i co metoda woła
   (dolny wykres **Callees**: `Regex..ctor`, `Regex.Replace`, `DateTime…`, `CultureInfo…`).
5. *(opcjonalnie, AI tylko do analizy)* Przepisz liczby jako tekst + kod metody →
   „Wyjaśnij, gdzie idzie czas i jaka jest hipoteza – bez proponowania kodu”.

### Na checkpoincie (2 min, po udostępnieniu `14-performance`)
`git stash -u && git fetch && git checkout 14-performance` → ten sam ślad tym samym narzędziem → w Sandwich wyszukaj
metodę wskazaną w kroku 4 – brak wyników. Przy wiązaniu wiersza zostaje tylko obsługa miniatur (`ThumbnailConverter`, `ThumbnailCache` – cache z
części 3 bloku; przy pierwszym przewinięciu pobiera i zapisuje pliki).

### Kryteria akceptacji (A)
- Wskazana jedna metoda naszego kodu z liczbami (Total metody i Total `n_OnBindViewHolder`) i jej wywołaniami z
  wykresu Callees.
- Wniosek własnymi słowami: gorący punkt **naszego** kodu, a nie przyczyna wszystkich klatek (resztę czasu zajmuje kod
  natywny listy).
- Na `14-performance`: wskazanej metody w śladzie nie ma.

---

## Ćwiczenie B – wyciek w kreatorze

**User story:** Jako technik kończący kilkanaście wizyt dziennie chcę, aby aplikacja nie zwalniała z każdą otwartą
wizytą, aby nie restartować jej w połowie dnia.

Konfiguracja **Debug**. Zaloguj się **nowym adresem** (np. `cw14.twoje-imie@szkolenie.pl`, PIN `1234`) – nowe konto
ma wizytę 8:00 „W trakcie”, więc kreator się otwiera. „Wejście” = szczegóły wizyty 8:00 → „Zakończ wizytę” → kreator →
wstecz (systemowe „wstecz” na Androidzie, strzałka w nagłówku na iOS).

### Zrzuty – Meteor (VS Code)
1. „Select project configuration” → **Debug**; Run and Debug → „Meteor: GC Dump” → **Ctrl+F5**.
2. 1 × wejście w kreator → Ustawienia → Diagnostyka → **„Wymuś GC”** → w Debug Console wpisz `/dump` → plik
   `src/FieldService.UI/pl.szkolenie.fieldservice.dev.gcdump` → **przemianuj na `T0.gcdump`** (następny `/dump`
   nadpisze plik).
3. Jeszcze 9 × wejście w kreator → „Wymuś GC” → `/dump` → przemianuj na `T1.gcdump`. Stop (Shift+F5).

### Zrzuty – wiersz poleceń (Android)
1. Router z ćwiczenia A (albo nowy: `dotnet-dsrouter server-server -tcps 127.0.0.1:9100`); zamknij aplikację TEST.
2. Build Debug z flagami diagnostyki:
   ```bash
   dotnet build src/FieldService.UI -f net10.0-android -c Debug -t:Install -p:AndroidEnableProfiler=true -p:DiagnosticAddress=10.0.2.2 -p:DiagnosticPort=9100 -p:DiagnosticSuspend=false -p:DiagnosticListenMode=connect
   ```
3. Aplikacja z ikony („FieldService DEV”) → 1 × wejście w kreator → „Wymuś GC” → `dotnet-gcdump collect -p <pid routera>
   -o T0.gcdump`.
4. Jeszcze 9 × wejście → „Wymuś GC” → `dotnet-gcdump collect -p <pid routera> -o T1.gcdump`.

### Analiza (oba narzędzia)
1. Co przybyło:
   ```bash
   dotnet run --project tools/gcdiff -- T0.gcdump T1.gcdump FieldService
   ```
   Szukasz typu, którego przybyło **tyle, ile było wejść** (9) i który jest „korzeniem” ekranu – ViewModelem
   (skrót: filtr `ViewModel` zamiast `FieldService`). Klienci API, bramy i handlery z przyrostem +18…+56 to zależności
   trzymane przez ten ViewModel, nie przyczyna.
2. Kto trzyma:
   ```bash
   dotnet run --project tools/retpath -- T1.gcdump Completion.CompleteVisitFlowViewModel
   ```
3. *(AI tylko do analizy)* Zapisz wyniki do plików (`… > gcdiff.txt`, `… > retpath.txt`) i daj LLM tekst obu
   wyników + kod `src/FieldService.Presentation/Visits/Completion/CompleteVisitFlowViewModel.cs`: „Które typy rosną
   między zrzutami i dlaczego? Co je trzyma? Podaj hipotezę i jak ją sprawdzić – bez pisania poprawki”. Odpowiedź to
   hipoteza: porównaj ją z kodem i ścieżką z `retpath`, zanim cokolwiek zmienisz.
4. Usuń `[Ignore("Ćwiczenie 14: …")]` (`git grep -n "Ćwiczenie 14" tests` – `LeakTests` i `CompleteVisitFlowSyncTests`)
   → `dotnet test --filter "FullyQualifiedName~CompleteVisitFlow"` – czerwono.
5. Napraw według `TODO` w konstruktorze `CompleteVisitFlowViewModel`.
6. `dotnet test` – zielono. Zbuduj i uruchom ponownie (ten sam sposób), 10 × wejście w kreator → „Wymuś GC” → zrzut
   `T2.gcdump` → `dotnet run --project tools/gcdiff -- T0.gcdump T2.gcdump ViewModel` i `retpath` na `T2.gcdump`.

### Kryteria akceptacji (B)
- Przed poprawką: `gcdiff T0 → T1` – `CompleteVisitFlowViewModel` 1 → 10 (+9, razem z czterema krokami kreatora);
  `retpath` – ścieżka od pola statycznego przez posłańca do kreatora.
- Po poprawce: `gcdiff T0 → T2` bez `CompleteVisitFlowViewModel`; `retpath` – „trzymanych przez korzenie GC: 0”;
  Diagnostyka po „Wymuś GC” – „kreator 0”.
- `dotnet test` zielony, bez `[Ignore]` z „Ćwiczenie 14”; `git grep -n StrongReferenceMessenger src` – pusto.

---

## iOS (symulator, opcjonalnie, tylko wiersz poleceń)
- Build: `dotnet build src/FieldService.UI -f net10.0-ios -c Test -p:RuntimeIdentifier=iossimulator-arm64
  -p:EnableDiagnostics=true` (ćwiczenie B: `-c Debug`, bez `EnableDiagnostics`), potem `xcrun simctl install booted
  src/FieldService.UI/bin/Test/net10.0-ios/iossimulator-arm64/FieldService.UI.app` (B: `bin/Debug/…`).
- Router z gniazdem: `dotnet-dsrouter server-server -ipcs /tmp/fs-ios.sock -tcps 127.0.0.1:9300`; aplikacja dostaje port
  przy uruchomieniu: `SIMCTL_CHILD_DOTNET_DiagnosticPorts=127.0.0.1:9300,nosuspend,connect xcrun simctl launch booted
  pl.szkolenie.fieldservice.test` (B: `pl.szkolenie.fieldservice.dev`).
- Narzędzia z `--diagnostic-port /tmp/fs-ios.sock,connect` zamiast `-p` (np. `dotnet-trace collect --diagnostic-port
  /tmp/fs-ios.sock,connect --duration 00:00:00:15 --format speedscope -o historia-ios.nettrace`, przewijanie ok. 3 s po
  starcie).
- W speedscope wiązanie wiersza to `GetCell` (zamiast `n_OnBindViewHolder`), wątek UI – zwykle 1.; na górze listy
  metod jest `Program.Main` (pętla aplikacji) i ładowanie strony historii – pomiń.

## Gdy nie działa
- Aplikacja z wiersza poleceń zawiesza się przy starcie (ANR) albo `Failed to collect gcdump`, a wcześniej
  używałeś Meteora: Meteor zostawia na emulatorze ustawienie `debug.mono.profile` (swój port). Wyczyść:
  `adb shell setprop debug.mono.profile "''"` i uruchom aplikację ponownie.
- `EndOfStreamException` – narzędzia 10.x; wróć do 9.0.661903.
- ANR / zawieszony start – router nie działa albo obsługuje inną aplikację z flagami diagnostycznymi: router najpierw,
  inne takie aplikacje zamknij.
- W speedscope tylko `UNMANAGED_CODE_TIME` – zły wątek albo przewijanie poza oknem `--duration`.
- `gcdiff` pokazuje przyrost **po** poprawce (np. +8 kreatorów), a `retpath` mówi „0”: zrzut bez „Wymuś GC” – to obiekty
  czekające na finalizację, nie wyciek (LLM łatwo weźmie je za wyciek). Zrób zrzut po „Wymuś GC”.
- `WeakReference<…BindableObject>` i `Ephemeron[]` rosną w każdym porównaniu – to słabe referencje MAUI, nie wyciek;
  filtruj `FieldService` albo `ViewModel`.
- Kreator zamyka się od razu – wizyta 8:00 tego konta nie jest „W trakcie” (np. zakończona wcześniej); zaloguj się nowym
  adresem.
- Pierwsze logowanie po świeżej instalacji bywa nieskuteczne – zamknij aplikację i zaloguj ponownie.
- `git status` pokazuje `*.speedscope.json` – plik wyniku śladu; nie commituj go (usuń albo przenieś poza repo).

## Typowe wpadki (B)
- Obiekt tworzony w tej samej metodzie co asercja – JIT trzyma referencję; tworzymy go w osobnej metodzie z
  `[MethodImpl(MethodImplOptions.NoInlining)]`.
- Jedno `GC.Collect()` – potrzebne `GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();`.
- `StrongReferenceMessenger.Default.UnregisterAll(this)` w `OnClosed()` – wyciek znika, ale kreator dalej nic nie
  dostaje (nadawca używa słabego posłańca) – testy komunikatu czerwone.

## Jeśli utkniesz
```bash
git stash -u && git fetch && git checkout 14-performance
```
