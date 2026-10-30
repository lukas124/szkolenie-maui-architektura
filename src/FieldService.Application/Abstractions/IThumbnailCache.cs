namespace FieldService.Application.Abstractions;

// Port: miniatura z adresu URL (lista wizyt, historia). Brak sieci i brak w cache → pusty strumień, nie wyjątek
public interface IThumbnailCache
{
    Task<Stream> OpenAsync(string url, CancellationToken ct);
}
