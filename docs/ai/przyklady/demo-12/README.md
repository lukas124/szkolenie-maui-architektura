# Demo 12 – gotowe odpowiedzi LLM (plan B)

Gdy LLM nie odpowiada albo odpowiada inaczej niż na próbie – te pliki zamiast odpowiedzi na żywo.
Te same zmiany jako łatki do nałożenia na `12-ui-ai-start`: `tools/demo/12-xaml-bez-ograniczen.patch`,
`tools/demo/12-xaml-z-ograniczeniami.patch` (`git apply …`, cofnięcie: `git checkout -- src/FieldService.UI/Parts`).

| Plik | Prompt | Co pokazuje |
|---|---|---|
| `01-vanstock-bez-ograniczen.xaml` (+ `.xaml.cs`) | makieta `../../makiety/van-stock.png` + „zrób ekran w XAML” | 5 problemów: kolory na sztywno, `StackLayout` w `StackLayout`, brak `x:DataType`, `Clicked` w code-behind, wymyślona `IsLoadingParts` (w ViewModelu jest `IsBusy`) – do tego `ListView` i `Frame` (przestarzałe) oraz napisy na sztywno zamiast `{loc:Translate Klucz}` |
| `02-vanstock-z-ograniczeniami.xaml` | makieta + `../../prompt-xaml.md` + `VanStockViewModel.cs` + style | dobry układ, ale jedna wymyślona właściwość (`IsLoadingParts` zamiast `IsRefreshing`) → `dotnet build -c Release` kończy się błędem XC0045 – „kompilator złapał halucynację” |
| `03-statusbadge-od-asystenta.xaml` (+ `.xaml.cs`) | „`StatusBadge` jako `ContentView` z `BindableProperty` `Tone` i `Text`” | `BindingContext = this`, `typeof(Color)` zamiast `VisitTone`, wartość domyślna `0` (wyjątek przy pierwszym użyciu) |

Wersje poprawne: `src/FieldService.UI/Parts/Pages/VanStockPage.xaml` i `src/FieldService.UI/Controls/StatusBadge.xaml(.cs)`
na gałęzi `12-ui-ai`.
