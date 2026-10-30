using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Presentation.Common;
using FieldService.Presentation.Diagnostics;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Settings;

// Ustawienia (ikona w nagłówku listy – jak w aplikacji z podstaw): technik, środowisko, wersja, Diagnostyka (Debug i Test),
// „Wyloguj”.
public sealed partial class SettingsViewModel(
    SignOutHandler signOut,
    ILocalTechnicianData localData,
    IPreferencesStore preferences,
    IAppEnvironment environment,
    IUserFeedback feedback,
    INavigationService navigation) : BaseViewModel, INavigatedTo<SettingsRoute>
{
    [ObservableProperty]
    public partial string TechnicianName { get; set; } = "";

    [ObservableProperty]
    public partial string Email { get; set; } = "";

    public string EnvironmentName => environment.EnvironmentName;

    public string VersionText => environment.VersionText;

    public bool IsDiagnostics => environment.IsDiagnostics;

    // technik zapisany przy logowaniu (SignInHandler)
    public Task OnNavigatedToAsync(SettingsRoute route)
    {
        Email = preferences.Get(SignInHandler.OwnerKey) ?? "";
        TechnicianName = preferences.Get(SignInHandler.TechnicianNameKey) ?? Email;
        return Task.CompletedTask;
    }

    // liczniki żądań, cache i żywe ViewModele do pomiarów
    [RelayCommand]
    private Task OpenDiagnosticsAsync() => navigation.NavigateAsync(new DiagnosticsRoute());

    // Wylogowanie: sesja i cache znikają; wizyty i niewysłane zmiany zostają w telefonie – wyśle je ten sam technik
    // po zalogowaniu (ostrzeżenie, gdy coś czeka)
    [RelayCommand]
    private async Task SignOutAsync(CancellationToken ct)
    {
        var message = await localData.HasUnsentChangesAsync(ct) ? "Settings_SignOutPendingMessage" : "Settings_SignOutMessage";
        if (!await feedback.ConfirmAsync("Settings_SignOutTitle", message))
            return;

        await signOut.HandleAsync(ct);
        await navigation.ResetToAsync(new LoginRoute());
    }
}
