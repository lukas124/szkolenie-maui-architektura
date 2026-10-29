# Prompt – ekran w XAML z makiety (z ograniczeniami)

**Cel:** dostać XAML, który przejdzie [listę przeglądu XAML](review-xaml.md) za pierwszym razem: wiąże się tylko
z tym, co jest w ViewModelu, jest płaski, korzysta ze stylów i działa w trybie jasnym, ciemnym i z dużą czcionką.
Bez ograniczeń ekran „na oko” jest gotowy w 30 sekund – i potem 30 minut poprawek.

**Kontekst do dołączenia** (pokaż pliki, nie opisuj ich):
- makieta, zrzut albo opis ekranu (np. [`makiety/van-stock.png`](makiety/van-stock.png)).
- **plik ViewModelu** z `src/FieldService.Presentation` (np. `Parts/VanStockViewModel.cs`) i model elementu listy
  (np. `src/FieldService.Application/Parts/Queries/GetVanStock.cs` – `VanStockItem`).
- `src/FieldService.UI/Resources/Styles/Styles.xaml` i `Colors.xaml`.
- `src/FieldService.UI/Resources/Strings/Strings.resx` i `Strings.en.resx` (napisy stron).
- `AGENTS.md`, `src/FieldService.UI/AGENTS.md`.
- wzorzec strony z projektu, np. `src/FieldService.UI/Orders/Pages/WorkOrdersPage.xaml` (+ `.xaml.cs`).

## Prompt

```text
Zrób stronę <NazwaPage>.xaml (+ .xaml.cs) w src/FieldService.UI/<Moduł>/Pages według makiety.
ViewModel: <NazwaViewModel> (plik w załączniku) – NIE zmieniaj go i NIE wymyślaj właściwości ani komend:
wiąż się wyłącznie z tym, co w nim jest. Jeśli czegoś brakuje do makiety – napisz to, zamiast dopisywać.

Wymagania (strona musi je spełnić):
- strona dziedziczy po pages:BasePage (jak <wzorzec strony>), Title i CanNavigateBack jak we wzorcu.
- x:DataType na stronie i w KAŻDYM DataTemplate (bindingi kompilowane).
- jeden Grid na wiersz listy (RowDefinitions/ColumnDefinitions), bez StackLayout w StackLayout.
- rozmiary: * i stałe dla elementów o stałym rozmiarze (ikona, miniatura), Auto tylko dla tekstu, bez HeightRequest na tekstach.
- lista: CollectionView (nie ListView, nie BindableLayout dla długich list), NIE w ScrollView. Równe wiersze → jeden
  szablon i ItemSizingStrategy="MeasureFirstItem".
- kolory, czcionki, marginesy tylko ze stylów i zasobów (StaticResource, AppThemeBinding Light/Dark) – zero "#RRGGBB".
- napisy (Text, Title, Placeholder, słowa w StringFormat) z zasobów: Text="{loc:Translate Ekran_Nazwa}",
  StringFormat={loc:TranslateFormat Ekran_Nazwa}, klucz dopisany do Strings.resx (polski) i Strings.en.resx
  (angielski) – zero napisów na sztywno.
- SemanticProperties.Description dla ikon, statusów i liczb, nagłówek sekcji z SemanticProperties.HeadingLevel.
- akcje przez Command z ViewModelu (zdarzenia → komendy). Code-behind = tylko konstruktor z InitializeComponent
  (BindingContext przypisuje stronie usługa nawigacji, nie code-behind).
- aktualne API .NET MAUI 10: Border (nie Frame), CollectionView, Grid (nie RelativeLayout), DeviceInfo (nie Device.*).

Nie wolno: logiki w code-behind, x:Name używanego z kodu, RelativeSource AncestorType w każdym wierszu, konwerterów
liczących (formatowanie dat, Regex, new CultureInfo), typów z FieldService.Domain w XAML.
Na końcu wypisz, które punkty listy przeglądu (docs/ai/review-xaml.md) spełniasz i czego nie dało się zrobić.
```

## Czego oczekiwać

- Strona z `x:DataType`, jednym `Grid` na wiersz i stylami – jak `src/FieldService.UI/Parts/Pages/VanStockPage.xaml`.
- **Halucynacja łapana przez kompilator:** właściwość, której nie ma w ViewModelu (np. `IsLoadingParts` zamiast
  `IsBusy`), przy `x:DataType` daje **błąd kompilacji XAML** – bez `x:DataType` przeszłaby po cichu (pusta etykieta).
- Kontrolka własna (`ContentView` + `BindableProperty`): LLM często wstawia `BindingContext = this` i typ
  `Color` zamiast semantycznego `VisitTone` – sprawdź punkt 10 listy przeglądu.

## Weryfikacja

```bash
dotnet build src/FieldService.UI -c Release -f net10.0-android   # zero ostrzeżeń XAML (TreatWarningsAsErrors)
git grep -n "#[0-9A-Fa-f]\{6\}" -- src/FieldService.UI/<Moduł>         # pusto – kolory tylko w Colors.xaml
```

Na emulatorze: zrzut w trybie jasnym, ciemnym i z dużą czcionką (polecenia w [`review-xaml.md`](review-xaml.md)).
