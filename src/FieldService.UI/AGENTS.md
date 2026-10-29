# FieldService.UI – instrukcje dla LLM (XAML)

Doprecyzowuje `AGENTS.md` z korzenia dla stron XAML. Kopia dla Copilota: `.github/instructions/xaml.instructions.md` –
zmieniasz jeden plik, zmieniasz oba.

- Każdy `DataTemplate` i strona mają `x:DataType` (kompilowane bindingi; zły binding wychodzi w buildzie Release).
- Komunikat, pytanie i wybór z listy – własne strony modalne (`Dialogs/DialogPage`, `Dialogs/ChoicePage`) przez
  `IUserFeedback`/`IDialogService`; bez systemowych `DisplayAlert`/`DisplayActionSheet` i bez `Picker` (wybór – przycisk
  z `ChooseAsync`).
- Binding do ViewModelu albo modelu prezentacji – bez logiki w code-behind (w code-behind tylko konstruktor
  z `InitializeComponent()`, a `BindingContext` przypisuje stronie `NavigationService`).
- Kolory i style z `Resources/Styles` (`StaticResource`), kolor statusu przez `ToneToColorConverter`, nie `Color` z ViewModelu.
- Napisy (`Text`, `Title`, `Placeholder`, słowa w `StringFormat`) z zasobów: `Text="{loc:Translate Ekran_Nazwa}"`,
  `StringFormat={loc:TranslateFormat Ekran_Nazwa}` (`xmlns:loc="clr-namespace:FieldService.UI.MarkupExtensions"`), klucz
  dopisany do `Resources/Strings/Strings.resx` (polski) i `Strings.en.resx` (angielski). Zero napisów na sztywno w XAML.
- Komenda z elementu listy: w modelu elementu (`VisitHistoryItem.OpenCommand` → `Command="{Binding OpenCommand}"`);
  `RelativeSource AncestorType` w każdym wierszu tylko tam, gdzie już jest (lista wizyt).
- Lista z danymi: `CollectionView` (wirtualizacja, nigdy w `ScrollView`); `BindableLayout` tylko dla kilku elementów;
  równe wiersze → jeden szablon + `ItemSizingStrategy="MeasureFirstItem"`.
- Wiersz listy: jeden `Grid` (styl `ListRow`); status przez kontrolkę `Controls/StatusBadge` (`Tone`, `Text`).
- Nowy ekran z makiety: prompt `docs/ai/prompt-xaml.md`, przegląd `docs/ai/review-xaml.md` (10 punktów).
- Nowa strona: dziedziczy po `BasePage`, rejestracja `AddPage<TPage, TViewModel>()` w stronach modułu (np.
  `src/FieldService.UI/Visits/VisitsPages.cs`) + trasa w module Presentation (`RouteMap`).
- Po zmianie XAML: `dotnet build src/FieldService.UI -c Release -f net10.0-android` (kompilowane bindingi).
