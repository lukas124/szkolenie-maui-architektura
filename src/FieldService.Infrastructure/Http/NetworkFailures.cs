using System.Net;

namespace FieldService.Infrastructure.Http;

// Czy wyjątek oznacza brak sieci albo brak odpowiedzi serwera?
// (Android zgłasza brak sieci także jako WebException albo IOException)
internal static class NetworkFailures
{
    public static bool IsNetwork(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or WebException or IOException;
}
