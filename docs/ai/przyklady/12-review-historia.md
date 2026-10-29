# Ćwiczenie 12 (wariant B) – odpowiedź „recenzenta” dla `VisitHistoryPage.xaml`

Prompt: „Przejrzyj `src/FieldService.UI/Visits/Pages/VisitHistoryPage.xaml` (+ `.xaml.cs`) według
`docs/ai/review-xaml.md` (10 punktów). Dla każdej uwagi: punkt listy, linia, poprawka.”

Odpowiedź (gałąź `12-ui-ai-start`) – przeczytaj ją krytycznie: część uwag jest trafna, część fałszywa, a jednego
ważnego problemu recenzent nie zauważył. Zaznacz przy każdej: **trafna / fałszywa**, a na końcu dopisz, czego brakuje.

---

1. **Punkt 1 – bindingi kompilowane.** Strona nie ma `x:DataType`, `DataTemplate` też nie, a w code-behind jest
   `[XamlCompilation(XamlCompilationOptions.Skip)]` – XAML jest parsowany w czasie działania, bindingi przez refleksję.
   Usuń atrybut, dodaj `x:DataType="visits:VisitHistoryViewModel"` na stronie i `x:DataType="visits:VisitHistoryItem"`
   w `DataTemplate`.
2. **Punkt 2 – płasko.** Wiersz ma cztery poziomy `StackLayout` (l. 18–29). Jeden `Grid` z kolumnami
   `56,*,Auto` i dwoma wierszami wystarczy.
3. **Punkt 8 – style.** Kolory na sztywno: `#212121`, `#757575`, `#D32F2F`, `#FFFFFF` (l. 13, 18, 23–28). W trybie
   ciemnym nagłówek „Twoje wizyty” jest czarny na ciemnym tle, a wiersze zostają białe (`#FFFFFF`) – ekran nie ma
   ciemnego motywu. Użyj stylów `CardTitle`, `Caption`, `ListRow` (`AppThemeBinding`).
   Napisy „Historia wizyt” i „Twoje wizyty” są na sztywno (l. 9, 13) – weź je z zasobów przez
   `{loc:Translate Klucz}`.
4. **Punkt 6 – bindingi bez szukania.** `RelativeSource AncestorType={x:Type ContentPage}` w każdym wierszu (l. 32),
   a `OpenVisitCommand` nie istnieje w `VisitHistoryViewModel` – dotknięcie wiersza nic nie robi, bez żadnego błędu.
   `VisitHistoryItem` ma własne `OpenCommand`: `Command="{Binding OpenCommand}"`.
5. **Punkt 5 – obrazy.** `Source="{Binding PhotoUrl}"` (l. 20) ładuje pełne zdjęcie 800 px do kafelka 56 px –
   w liście użyj `ThumbnailUrl`.
6. **Punkt 9 – dostępność.** Status (l. 24) wyróżnia tylko kolor, bez `SemanticProperties.Description` – zrób go
   kontrolką `StatusBadge` z opisem = tekst statusu. Opisu nie dawaj na cały wiersz: zasłoniłby czytnikowi ekranu
   status i datę.
7. **Punkt 4 – kolekcje.** Dla płynności zamień `CollectionView` na `ListView` z
   `CachingStrategy="RecycleElement"` – `ListView` lepiej recyklinguje komórki.
8. **Punkt 3 – rozmiary.** Wiersze mają różną wysokość, co spowalnia przewijanie: dodaj `HeightRequest="80"` do etykiet
   w wierszu, żeby wszystkie miały stałą wysokość.

---

## Klucz (dla prowadzącego)

| Uwaga | Ocena | Dlaczego |
|---|---|---|
| 1–6 | trafne | zgodne z listą przeglądu |
| 7 | **fałszywa** | przestarzała rada z czasów Xamarin – w .NET MAUI `ListView` jest przestarzały; `CollectionView` wirtualizuje sam, równe wiersze daje jeden szablon + `ItemSizingStrategy="MeasureFirstItem"` |
| 8 | **fałszywa** | odwrotnie: `HeightRequest` na tekstach jest błędem (ucina tekst przy `font_scale 1.3`) – usuń istniejące `HeightRequest` z etykiet |
| pominięta | **`ScrollView` wokół `CollectionView`** (l. 11) | `ScrollView` daje liście nieskończoną wysokość – `CollectionView` tworzy wszystkie wiersze naraz, wirtualizacja przestaje działać; to główna przyczyna klatkowania |

„Przegląd daje hipotezy, pomiar – dowód”: po poprawkach `gfxinfo` w Release (karta ćwiczenia, krok 4).
