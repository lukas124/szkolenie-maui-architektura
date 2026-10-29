using FieldService.Application.Abstractions;

namespace FieldService.UI.Platform;

// Adapter portu IPreferencesStore: Preferences z MAUI
public sealed class MauiPreferencesStore : IPreferencesStore
{
    public string? Get(string key) => Preferences.Default.Get<string?>(key, null);

    public void Set(string key, string value) => Preferences.Default.Set(key, value);
}
