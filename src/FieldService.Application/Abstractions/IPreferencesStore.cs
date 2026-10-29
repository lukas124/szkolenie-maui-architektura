namespace FieldService.Application.Abstractions;

// Port: małe ustawienia zapamiętane w telefonie (np. ostatni e-mail logowania)
public interface IPreferencesStore
{
    string? Get(string key);
    void Set(string key, string value);
}
