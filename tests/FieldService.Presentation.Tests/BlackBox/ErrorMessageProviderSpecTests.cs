using FieldService.Domain.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>
/// Dostawca komunikatów: najpierw tekst dla kodu (Error_{Code}), potem dla typu (Error_{Type}),
/// na końcu Error_Unexpected.
/// </summary>
[TestFixture]
public class ErrorMessageProviderSpecTests
{
    private readonly ErrorMessageProvider _provider = new(new SpecLocalizer());

    [Test]
    public void TekstDlaKodu_MaPierwszenstwoPrzedTypem()
    {
        var message = _provider.GetMessage(new Error("Auth.InvalidCredentials", ErrorType.Unauthorized));

        Assert.That(message, Is.EqualTo(SpecLocalizer.ErrorInvalidCredentials));
    }

    [Test]
    public void TekstDlaKodu_NiezaleznieOdTypu()
    {
        var message = _provider.GetMessage(new Error("Auth.InvalidCredentials", ErrorType.Validation));

        Assert.That(message, Is.EqualTo(SpecLocalizer.ErrorInvalidCredentials));
    }

    [TestCase(ErrorType.Network, SpecLocalizer.ErrorNetwork)]
    [TestCase(ErrorType.Unauthorized, SpecLocalizer.ErrorUnauthorized)]
    [TestCase(ErrorType.Unexpected, SpecLocalizer.ErrorUnexpected)]
    public void BrakTekstuDlaKodu_TekstDlaTypu(ErrorType type, string expected)
    {
        var message = _provider.GetMessage(new Error("Visits.SomethingUnknown", type));

        Assert.That(message, Is.EqualTo(expected));
    }

    [TestCase(ErrorType.Conflict)]
    [TestCase(ErrorType.NotFound)]
    [TestCase(ErrorType.Validation)]
    public void BrakTekstuDlaKoduITypu_Unexpected(ErrorType type)
    {
        var message = _provider.GetMessage(new Error("Visits.SomethingUnknown", type));

        Assert.That(message, Is.EqualTo(SpecLocalizer.ErrorUnexpected));
    }

    [Test]
    public void NigdyNieZwracaSurowegoKlucza()
    {
        var message = _provider.GetMessage(new Error("Zupelnie.Nieznany", ErrorType.Conflict));

        Assert.That(message, Does.Not.StartWith("Error_"));
        Assert.That(message, Does.Not.Contain("Zupelnie.Nieznany"));
    }

    [Test]
    public void KodSpozaSlownikaDlaInnegoKodu_NieDajeTekstuInnegoKodu()
    {
        // Kod „Auth.Other” nie ma tekstu – nie wolno podstawić tekstu dla „Auth.InvalidCredentials”.
        var message = _provider.GetMessage(new Error("Auth.Other", ErrorType.Unauthorized));

        Assert.That(message, Is.EqualTo(SpecLocalizer.ErrorUnauthorized));
    }
}
