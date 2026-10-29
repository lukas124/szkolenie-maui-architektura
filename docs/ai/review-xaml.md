# Lista przeglądu XAML

Ta sama lista dla XAML od LLM i od człowieka. Przegląd daje hipotezy – dowód daje kompilator (`x:DataType`),
zrzuty i pomiar klatek. Jeden punkt „nie” = poprawka przed commitem.

1. **Bindingi kompilowane.** `x:DataType` na stronie i w każdym `DataTemplate`; zero ostrzeżeń kompilatora XAML
   (np. XC0022 – binding bez `x:DataType`); brak `XamlCompilation(Skip)`.
2. **Płasko.** Jeden `Grid` na wiersz albo sekcję; brak `StackLayout` w `StackLayout` (w wierszu listy ≤ 3 poziomy).
3. **Rozmiary.** `*` i stałe zamiast `Auto`, gdy treść ma znany rozmiar (ikona, miniatura); brak `HeightRequest`
   na tekstach (duża czcionka musi się zmieścić).
4. **Kolekcje.** `CollectionView` dla list, nigdy w `ScrollView` (wirtualizacja); `BindableLayout` tylko dla kilku
   elementów; równe wiersze → jeden szablon i `ItemSizingStrategy="MeasureFirstItem"` (z `DataTemplateSelector`
   `MeasureFirstItem` nie działa – wariant wiersza przez `DataTrigger`); długie listy – `RemainingItemsThreshold`
   i stronicowane zapytanie; dopisywanie paczkami (`BatchObservableCollection.AddRange` – jedno powiadomienie).
5. **Obrazy.** Miniatury w rozmiarze wyświetlania (`ThumbnailUrl`, nie pełne zdjęcie), `Aspect`, stałe
   `WidthRequest`/`HeightRequest` obrazu.
6. **Bindingi bez szukania.** Brak `RelativeSource AncestorType` i `x:Reference` do strony w każdym wierszu – komenda
   w modelu elementu (`VisitHistoryItem.OpenCommand`); brak bindingów do nieistniejących właściwości.
7. **Konwertery i selektory.** Konwerter nie liczy (bez `new CultureInfo`, `Regex`, formatowania dat – gotowy tekst
   z fabryki w `Presentation`); selektor trzyma szablony w polach (`VisitTemplateSelector`), nie tworzy ich przy
   każdym wierszu.
8. **Style.** Kolory, marginesy i czcionki z zasobów (`Styles.xaml`, `Colors.xaml`), `AppThemeBinding` dla jasnego
   i ciemnego; brak wartości „hex” na stronie. Napisy z zasobów przez `{loc:Translate Klucz}` (`Strings.resx`,
   `Strings.en.resx`), nie na sztywno.
9. **Dostępność.** `SemanticProperties.Description` dla ikon, statusów i liczb, `HeadingLevel` dla nagłówków,
   cele dotyku ≥ 44 pt (iOS) / 48 dp (Android), czytelność przy `font_scale 1.3`.
10. **Code-behind, kontrolki i warstwy.** Code-behind strony = tylko konstruktor; zdarzenia → komendy; brak `x:Name`
    używanego z kodu do logiki; typy z `Presentation`, nie z `Domain`; własna kontrolka: `BindableProperty` z typem
    semantycznym (`VisitTone`, nie `Color`), poprawna wartość domyślna, wewnątrz bindingi przez `x:Reference` do
    korzenia kontrolki – **nigdy `BindingContext = this`**; przestarzałe API wymienione (`Frame` → `Border`,
    `ListView` → `CollectionView`, `RelativeLayout` → `Grid`, `Device.*` → `DeviceInfo`/`MainThread`).

## Polecenia

```bash
dotnet build src/FieldService.UI -c Release -f net10.0-android           # punkt 1: zero ostrzeżeń
git grep -n "#[0-9A-Fa-f]\{6\}" -- src/FieldService.UI/Visits src/FieldService.UI/Parts   # punkt 8: pusto
git grep -n "BindingContext = this" -- src/FieldService.UI/Controls      # punkt 10: pusto

# zrzuty: jasny, ciemny, duża czcionka (punkty 8–9)
adb exec-out screencap -p > docs/ai/zrzuty/historia-po-jasny.png
adb shell cmd uimode night yes && adb exec-out screencap -p > docs/ai/zrzuty/historia-po-ciemny.png
adb shell cmd uimode night no
adb shell settings put system font_scale 1.3 && adb exec-out screencap -p > docs/ai/zrzuty/historia-po-czcionka-1.3.png
adb shell settings put system font_scale 1.0

# płynność i pamięć (punkty 2–5) – tylko build Release (pakiet bez sufiksu .dev)
adb shell dumpsys gfxinfo pl.szkolenie.fieldservice reset
# … otwórz historię i przewiń ją kilkanaście razy …
adb shell dumpsys gfxinfo pl.szkolenie.fieldservice | grep -E "Janky frames|90th percentile"
adb shell dumpsys meminfo pl.szkolenie.fieldservice | grep "TOTAL PSS"   # bez wirtualizacji – wyraźnie więcej
```
