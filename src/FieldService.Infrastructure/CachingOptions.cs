namespace FieldService.Infrastructure;

// Ustawienia cache i pomiarów (z korzenia kompozycji)
public sealed class CachingOptions
{
    // licznik żądań HTTP w potoku (Diagnostyka – Debug i Test)
    public bool CountRequests { get; init; }
}
