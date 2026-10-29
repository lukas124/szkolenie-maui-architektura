# FieldService — aplikacja szkoleniowa „Architektura aplikacji w .NET MAUI”

Repozytorium z kodem do 2-dniowego szkolenia **Architektura aplikacji w .NET MAUI** (Czysta Architektura, MVVM i
taktyczne DDD w praktyce). Budujemy krok po kroku aplikację **FieldService**: technik serwisowy realizuje wizyty u
klientów (czynności, zużyte części, podpis klienta, protokół), często bez zasięgu, a dyspozytor planuje i anuluje
wizyty w panelu.

## Struktura rozwiązania

```
FieldService.slnx
├── src/
│   ├── FieldService.Domain/          # model domeny – reguły biznesowe; zero zależności
│   ├── FieldService.Application/     # przypadki użycia i porty; zna tylko Domain
│   ├── FieldService.Infrastructure/  # adaptery: SQLite, HTTP, cache; czysty .NET
│   ├── FieldService.Presentation/    # ViewModele i dane dla widoku; czysty .NET, BEZ MAUI
│   └── FieldService.UI/              # jedyny projekt MAUI: strony, platformy, MauiProgram (korzeń kompozycji)
├── tests/                            # NUnit + Moq, net10.0 (bez emulatora)
└── docs/cwiczenia/                   # treść ćwiczeń do każdego bloku
```

Zależności wskazują tylko do środka (reguła zależności): `UI` → `Presentation` i `Infrastructure` → `Application` →
`Domain`. **Tylko `FieldService.UI` zna MAUI.**

## Wymagania

- .NET 10 SDK (10.0.100+) i workload MAUI: `dotnet workload install maui-android` (na Macu także `maui-ios`)
- Android SDK + emulator (API 35 lub 36) albo telefon z włączonym debugowaniem USB
- opcjonalnie Mac z Xcode (iOS)
- IDE: Visual Studio 2026, JetBrains Rider albo VS Code z C# Dev Kit + .NET MAUI
- Git

## Jak uruchomić

```bash
# testy (bez emulatora)
dotnet test

# Android: build, instalacja i uruchomienie (-t:Run) na emulatorze lub podłączonym telefonie
dotnet build src/FieldService.UI -t:Run -f net10.0-android

# iOS: to samo na symulatorze (tylko macOS; tam target net10.0-ios jest dodawany automatycznie)
dotnet build src/FieldService.UI -t:Run -f net10.0-ios
```

Logowanie w aplikacji: dowolny adres e-mail (każdy adres to osobny technik z własnymi wizytami) i PIN `1234`.
API szkoleniowe: https://mobilny.dev/maui/architektura/ (Swagger: `/swagger`).

Logi aplikacji na Androidzie: `adb logcat -s DOTNET`.

## Checkpointy

Każdy blok ćwiczeń ma dwie gałęzie:

- **`NN-nazwa-start`** — punkt startowy ćwiczenia: stan poprzedniego checkpointu + elementy przygotowane w szablonie.
  Miejsca do uzupełnienia są oznaczone komentarzem `TODO:`, a testy rozwiązania mają atrybut
  `[Ignore("Ćwiczenie NN: …")]`. Gałąź startowa zawsze się buduje; po uzupełnieniu TODO i usunięciu `[Ignore]` testy
  przechodzą.
- **`NN-nazwa`** (gałąź i tag) — **rozwiązanie ćwiczenia** z danego bloku i stan, od którego zaczyna się następny blok.

Blok 01 zaczyna się od `01-start`. Treść ćwiczeń: [`docs/cwiczenia/`](docs/cwiczenia/).

Repozytorium rośnie w trakcie szkolenia: kolejne gałęzie pojawiają się, gdy dojdziemy do danego bloku. Przed
przejściem na nową gałąź zrób `git fetch`:

```bash
git fetch
git switch 02-clean-architecture-start
```

Jeśli utkniesz w ćwiczeniu: `git stash && git fetch && git switch NN-nazwa` (gotowe rozwiązanie bloku).
