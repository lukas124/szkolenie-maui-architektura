using System.Windows.Input;
using FieldService.Presentation.Common;
using FieldService.UI.Dialogs;
using FieldService.UI.Extensions;

namespace FieldService.UI.Pages;

// Strona z nagłówkiem, która przekazuje swój cykl życia do ViewModelu (metoda szablonowa: BasePage woła haki,
// ViewModel je wypełnia). Wygląd nagłówka opisuje szablon PageTemplate (Templates/PageTemplate.xaml) – taki sam
// na Androidzie i iOS. Strona ustawia tylko treść: tytuł (Title), strzałkę „wstecz” (CanNavigateBack)
// i przycisk akcji z prawej (ActionIcon, ActionCommand). Strona bez nagłówka (np. logowanie): IsHeaderVisible = false.
public abstract class BasePage : ContentPage
{
    public static readonly BindableProperty IsHeaderVisibleProperty =
        BindableProperty.Create(nameof(IsHeaderVisible), typeof(bool), typeof(BasePage), true);

    public static readonly BindableProperty CanNavigateBackProperty =
        BindableProperty.Create(nameof(CanNavigateBack), typeof(bool), typeof(BasePage), false);

    // Zmiana ikony zmienia też HasAction (szablon pokazuje przycisk akcji tylko, gdy jest ikona)
    public static readonly BindableProperty ActionIconProperty =
        BindableProperty.Create(nameof(ActionIcon), typeof(ImageSource), typeof(BasePage),
            propertyChanged: (page, _, _) => ((BasePage)page).OnPropertyChanged(nameof(HasAction)));

    public static readonly BindableProperty ActionCommandProperty =
        BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(BasePage));

    public bool IsHeaderVisible
    {
        get => (bool)GetValue(IsHeaderVisibleProperty);
        set => SetValue(IsHeaderVisibleProperty, value);
    }

    public bool CanNavigateBack
    {
        get => (bool)GetValue(CanNavigateBackProperty);
        set => SetValue(CanNavigateBackProperty, value);
    }

    public ImageSource? ActionIcon
    {
        get => (ImageSource?)GetValue(ActionIconProperty);
        set => SetValue(ActionIconProperty, value);
    }

    public bool HasAction => ActionIcon is not null;

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    // Szablon (styl z App.xaml) jest nakładany już w konstruktorze ContentPage, zanim wykona się kod BasePage,
    // dlatego komendę tworzymy przy pierwszym odczycie, a nie w konstruktorze
    private Command? _backButtonCommand;

    // Strzałka „wstecz” w nagłówku = systemowy przycisk „wstecz”
    public ICommand BackButtonCommand => _backButtonCommand ??= new Command(() => OnBackRequestedAsync().FireAndForget());

    protected virtual Task OnBackRequestedAsync() => Navigation.PopAsync();

    // true = stronę przykrył dialog (DialogPage), a jego otwarcie i zamknięcie nie jest dla ViewModelu wyjściem ani powrotem
    private bool _coveredByDialog;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByDialog) { _coveredByDialog = false; return; }
        if (BindingContext is BaseViewModel vm) vm.OnAppearingAsync().FireAndForget();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Navigation.ModalStack.LastOrDefault() is DialogPage) { _coveredByDialog = true; return; }
        if (BindingContext is BaseViewModel vm) vm.OnDisappearingAsync().FireAndForget();
    }

    protected override bool OnBackButtonPressed()
    {
        // Korzeń stosu: oddajemy przycisk systemowi („wstecz” z listy zamyka aplikację)
        if (Navigation.NavigationStack.Count <= 1 && Navigation.ModalStack.Count == 0)
            return base.OnBackButtonPressed();

        OnBackRequestedAsync().FireAndForget();
        return true;
    }
}
