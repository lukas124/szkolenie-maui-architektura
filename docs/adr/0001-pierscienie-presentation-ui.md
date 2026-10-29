# 0001. Pięć projektów: Presentation bez MAUI, UI jako jedyny projekt MAUI

- **Status:** przyjęta
- **Data:** 2026-11-12

## Kontekst
W pierwszej wersji (gałąź `01-typowe-mvvm`) ViewModele, dostęp do API i SQLite były w jednym projekcie MAUI.
ViewModel znał `Color`, `Application.Current` i `HttpClient`, więc testy wymagały emulatora, a zmiana API albo wyglądu
dotykała tych samych klas. Potrzebujemy testów logiki bez emulatora i granic, których nie da się przekroczyć przypadkiem.

## Rozważane możliwości
1. Jeden projekt MAUI z folderami „warstw” – najprościej, ale granice pilnuje tylko dyscyplina; ViewModel dalej może
   użyć typów MAUI.
2. Trzy projekty (Core, Infrastructure, App) – ViewModele w projekcie MAUI, więc testy ViewModeli dalej z MAUI.
3. Pięć projektów: Domain, Application, Infrastructure, Presentation (`net10.0`, bez MAUI), UI (MAUI) – reguła
   zależności pilnowana przez kompilator i testy architektury.

## Decyzja
Rozwiązanie ma pięć projektów. `FieldService.Presentation` (ViewModele) jest `net10.0` bez MAUI, `FieldService.UI` jest
jedynym projektem MAUI i korzeniem kompozycji. Zależności: `UI` → `Presentation` i `Infrastructure` → `Application` →
`Domain`; pilnują ich testy w `tests/FieldService.Tests/LayerDependencyTests.cs`.

## Konsekwencje
- Pozytywne: ViewModele i przypadki użycia testujemy bez emulatora; typ MAUI w ViewModelu nie kompiluje się.
- **Negatywne:** więcej projektów i plików do nawigacji; prosta zmiana ekranu często dotyka dwóch projektów
  (ViewModel w Presentation, XAML w UI); kolor statusu wymaga semantycznego `VisitTone` i konwertera.
- Do zrobienia: nowe porty prezentacji (np. dialog) definiujemy w Presentation, adaptery w UI.
