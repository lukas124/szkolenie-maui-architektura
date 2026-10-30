namespace FieldService.Infrastructure;

// Ustawienia cache (z korzenia kompozycji)
public sealed class CachingOptions
{
    // katalog cen w pamięci – potem pytamy API (z ETagiem: 304, gdy nic się nie zmieniło)
    public TimeSpan CatalogTtl { get; init; } = TimeSpan.FromMinutes(5);

    // limit pamięci na miniatury; 0 = bez limitu (tylko do pomiaru „przed”)
    public long ThumbnailMemoryLimitBytes { get; init; } = 50 * 1024 * 1024;

    // licznik żądań HTTP w potoku (Diagnostyka – Debug i Test)
    public bool CountRequests { get; init; }
}
