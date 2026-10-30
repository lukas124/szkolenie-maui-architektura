namespace FieldService.Application.Abstractions;

// Port: sesja wygasła i nie dało się jej odświeżyć – UI pokazuje komunikat i ekran logowania.
// Dane w telefonie (outbox) zostają: ten sam technik po zalogowaniu wysyła je dalej.
public interface ISessionExpiry
{
    void OnExpired();
}
