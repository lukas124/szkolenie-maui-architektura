using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Session;

// Logowanie technika: dowolny e-mail + PIN 1234. Ostatni e-mail zapamiętany w telefonie.
public partial class LoginViewModel(
    SignInHandler signIn,
    IPreferencesStore preferences,
    IErrorMessageProvider messages,
    INavigationService navigation,
    StartupNavigator startup,
    RestoreSessionHandler restore) : BaseViewModel
{
    private const string EmailKey = "last_email";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Email { get; set; } = preferences.Get(EmailKey) ?? string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Pin { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    // true = start aplikacji już sprawdził zapamiętaną sesję
    private bool _restoreTried;

    // Start: zapamiętana sesja (refresh token) → od razu lista, bez logowania
    public override async Task OnAppearingAsync()
    {
        if (_restoreTried) return;
        _restoreTried = true;

        SetBusy(true); // w trakcie przywracania logowanie czeka – inaczej przywrócona sesja nadpisałaby nową
        try
        {
            if (!await restore.HandleAsync(CancellationToken.None)) return;
        }
        finally
        {
            SetBusy(false);
        }
        await navigation.ResetToAsync(new VisitsListRoute());
        await startup.ResumeAsync();
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        SignInCommand.NotifyCanExecuteChanged();
    }

    private bool CanSignIn() => !IsBusy && Email.Contains('@') && Pin.Length == 4;

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        var result = await signIn.HandleAsync(new SignIn(Email, Pin), ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = messages.GetMessage(result.Error!);
            return;
        }

        preferences.Set(EmailKey, Email.Trim());
        Pin = string.Empty;
        await navigation.ResetToAsync(new VisitsListRoute());
        await startup.ResumeAsync();
    }
}
