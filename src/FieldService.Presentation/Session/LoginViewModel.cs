using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Session;

// Logowanie technika: dowolny e-mail + PIN 1234. Ostatni e-mail zapamiętany w telefonie.
public partial class LoginViewModel(SignInHandler signIn, IPreferencesStore preferences, ILocalizer localizer) : ObservableObject
{
    private const string EmailKey = "last_email";

    // Po udanym logowaniu korzeń kompozycji (App) pokazuje listę wizyt
    public event EventHandler? SignedIn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Email { get; set; } = preferences.Get(EmailKey) ?? string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Pin { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    private bool CanSignIn() => Email.Contains('@') && Pin.Length == 4;

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        var result = await signIn.HandleAsync(new SignIn(Email, Pin), ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error!.Type switch
            {
                ErrorType.Unauthorized => localizer["Login_InvalidCredentials"],
                ErrorType.Network => localizer["Error_Network"],
                _ => localizer["Error_Unexpected"]
            };
            return;
        }

        preferences.Set(EmailKey, Email.Trim());
        Pin = string.Empty;
        SignedIn?.Invoke(this, EventArgs.Empty);
    }
}
