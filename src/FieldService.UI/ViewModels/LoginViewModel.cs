using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.UI.Services;

namespace FieldService.UI.ViewModels;

// Logowanie technika. Ekran logowania nie jest częścią ćwiczenia z Bloku 01 – porządkujemy go w Bloku 13 (sesja).
public partial class LoginViewModel(ApiSession session) : ObservableObject
{
    private const string EmailKey = "last_email";

    // Strona przechodzi do listy wizyt po udanym logowaniu
    public event EventHandler? SignedIn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Email { get; set; } = Preferences.Default.Get(EmailKey, string.Empty);

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
        var error = await session.SignInAsync(Email.Trim(), Pin, ct);
        if (error is not null)
        {
            ErrorMessage = error;
            return;
        }

        Preferences.Default.Set(EmailKey, Email.Trim());
        Pin = string.Empty;
        SignedIn?.Invoke(this, EventArgs.Empty);
    }
}
