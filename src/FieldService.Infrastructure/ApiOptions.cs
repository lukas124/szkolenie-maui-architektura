namespace FieldService.Infrastructure;

// Adres API szkoleniowego (z appsettings.json, przekazany z korzenia kompozycji)
public sealed class ApiOptions
{
    public required string BaseUrl { get; init; }
}
