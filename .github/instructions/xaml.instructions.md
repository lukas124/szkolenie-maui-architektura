---
applyTo: "**/*.xaml"
---
Ta sama treść co `src/FieldService.UI/AGENTS.md` (format GitHub Copilot) – zmieniasz jeden plik, zmieniasz oba.

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
- Komenda z elementu listy: `Command="{Binding …Command, Source={RelativeSource AncestorType={x:Type …ViewModel}}, x:DataType=…ViewModel}"`
  z `CommandParameter="{Binding .}"`.
- Lista z danymi: `CollectionView` (wirtualizacja); `BindableLayout` tylko dla kilku elementów.
- Nowa strona: dziedziczy po `BasePage`, rejestracja `AddPage<TPage, TViewModel>()` w stronach modułu (np.
  `src/FieldService.UI/Visits/VisitsPages.cs`) + trasa w module Presentation (`RouteMap`).
- Po zmianie XAML: `dotnet build src/FieldService.UI -c Release -f net10.0-android` (kompilowane bindingi).
