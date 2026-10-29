namespace FieldService.Application.Abstractions;

// Port: dane technika zapisane w telefonie (wizyty, zdjęcia). Telefon firmowy przechodzi między technikami –
// następny nie może zobaczyć danych poprzedniego ani zgubić jego niewysłanych zmian.
public interface ILocalTechnicianData
{
    // zmiany technika, które jeszcze nie dotarły na serwer
    Task<bool> HasUnsentChangesAsync(CancellationToken ct);

    // usuwa wszystkie dane technika z telefonu
    Task ClearAsync(CancellationToken ct);
}
