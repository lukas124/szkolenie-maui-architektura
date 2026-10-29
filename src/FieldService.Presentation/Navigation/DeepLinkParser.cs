using FieldService.Domain.Visits;

namespace FieldService.Presentation.Navigation;

// Adres z zewnątrz (powiadomienie, SMS, inna aplikacja) → ta sama trasa, co z przycisku
public static class DeepLinkParser
{
    public const string Scheme = "fieldservice";

    // fieldservice://visits → lista, fieldservice://visits/{id} → szczegóły, inny adres → null
    public static IRoute? Parse(Uri uri)
    {
        if (uri.Scheme != Scheme || uri.Host != "visits") return null;

        var id = uri.AbsolutePath.Trim('/');
        if (id.Length == 0) return new VisitsListRoute();
        return Guid.TryParse(id, out var guid) ? new VisitDetailsRoute(new VisitId(guid)) : null;
    }
}
