using FieldService.Domain.Common;

namespace FieldService.Infrastructure.Api;

// Błędy transportu wspólne dla wszystkich bram API
public static class ApiErrors
{
    // brak sieci, brak odpowiedzi w czasie, otwarty bezpiecznik
    public static readonly Error Network = new("Api.Network", ErrorType.Network);

    // odpowiedź to nie to, czego oczekujemy (np. Wi-Fi z portalem logowania zwraca stronę HTML)
    public static readonly Error InvalidResponse = new("Api.InvalidResponse", ErrorType.Unexpected);
}
