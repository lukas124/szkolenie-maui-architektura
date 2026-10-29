using FieldService.Domain.Common;

namespace FieldService.Domain.Visits;

// Obiekt wartości: kto podpisał, kiedy i POD KTÓRĄ WERSJĄ ZAKRESU – podpis dotyczy faktycznie wykonanego zakresu prac
public sealed record CustomerSignature
{
    private CustomerSignature(string signerName, DateTimeOffset signedAt, int scopeVersion)
    {
        SignerName = signerName;
        SignedAt = signedAt;
        ScopeVersion = scopeVersion;
    }

    public string SignerName { get; }
    public DateTimeOffset SignedAt { get; }
    public int ScopeVersion { get; }

    // TODO: Ćwiczenie 07 – pusty podpisujący → VisitErrors.SignerRequired; imię bez spacji na brzegach
    public static Result<CustomerSignature> Create(string signerName, DateTimeOffset signedAt, int scopeVersion) =>
        Result<CustomerSignature>.Success(new CustomerSignature(signerName, signedAt, scopeVersion));

    // Odtworzenie zapisanego podpisu (repozytorium)
    public static CustomerSignature Restore(string signerName, DateTimeOffset signedAt, int scopeVersion) =>
        new(signerName, signedAt, scopeVersion);
}
