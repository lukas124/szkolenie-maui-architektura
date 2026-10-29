# Ćwiczenie 01 — znajdź problemy

**Blok:** 01 · Szybki start: specyfika projektów MAUI i typowe MVVM · dzień 1 · **Start:** gałąź `01-typowe-mvvm`
· **Czas:** 12 min · indywidualnie, bez pisania kodu

## User story
Jako nowy programista w zespole chcę w 12 minut wskazać, co w ekranie listy wizyt utrudni zmiany i testy, aby
wiedzieć, od czego zacząć porządki.

## Start
```bash
git fetch && git checkout 01-typowe-mvvm
dotnet build src/FieldService.UI -t:Run -f net10.0-android
```
Zaloguj się dowolnym adresem e-mail (każdy adres to osobny technik z własnymi wizytami) i PIN-em `1234`.
Ekran działa: lista wizyt na dziś, szczegóły wizyty, „Rozpocznij wizytę”, tryb offline.

Pliki do przeglądu:
- `src/FieldService.UI/ViewModels/VisitsViewModel.cs`
- `src/FieldService.UI/Pages/VisitsPage.xaml.cs`
- `src/FieldService.UI/Models/VisitItem.cs`
- `src/FieldService.UI/Helpers/ServiceHelper.cs`

Logowanie (`LoginPage`, `LoginViewModel`, `ApiSession`) nie jest częścią ćwiczenia.

## Polecenie
Wypełnij tabelę: co najmniej **8 wierszy**.

| Plik:linia | Problem – co utrudni zmianę albo test? | Zasada, którą łamie | Jak poprawić (jednym zdaniem) |
|---|---|---|---|
| | | | |

Nazwy zasad do wyboru: SRP, DIP, testowalność, skromny obiekt (Humble Object), lokalizator usług (Service Locator),
`async void`, dostęp statyczny, zależność od frameworka.

Galeria na slajdach pokazała część wzorców i ich poprawki – teraz wskaż konkretne linie w kodzie. Poszukaj też
problemów, których na slajdach nie było.

## Kryteria akceptacji
- Co najmniej 8 wierszy, każdy z nazwą zasady.
- Potrafisz wskazać 2 problemy, które usunie samo CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`).

## Podpowiedzi
- Pytaj „co utrudni zmianę albo test?”, nie „co mi się nie podoba stylistycznie?” – nazwy i formatowanie pomijamy.
- Wyobraź sobie test jednostkowy `VisitsViewModel`: czego potrzebuje, żeby w ogóle wystartować?
- Co się stanie, gdy technik dwa razy szybko odświeży listę? A gdy wyjątek poleci z `LoadData`?
- Kto i kiedy odpina się od zdarzenia `VisitsChanged`?
