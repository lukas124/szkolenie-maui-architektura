using FieldService.Application.Abstractions;
using FieldService.Application.Session;
using FieldService.Domain.Common;

namespace FieldService.Application.Tests.BlackBox;

/// <summary>Logowanie: przycięty e-mail trafia do bramy, wynik bramy wraca bez zmian.</summary>
[TestFixture]
public class SignInHandlerSpecTests
{
    private Mock<IAuthGateway> _auth = null!;
    private SignInHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _auth = new Mock<IAuthGateway>();
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("token-123"));
        _handler = new SignInHandler(_auth.Object);
    }

    [TestCase("  jan@firma.pl  ")]
    [TestCase("jan@firma.pl\t")]
    [TestCase("\njan@firma.pl")]
    public async Task PrzycinaEmail_PrzedWyslaniem(string email)
    {
        await _handler.HandleAsync(new SignIn(email, "1234"), CancellationToken.None);

        _auth.Verify(a => a.SignInAsync("jan@firma.pl", "1234", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Sukces_ZwracaTokenZBramy()
    {
        var result = await _handler.HandleAsync(new SignIn("jan@firma.pl", "1234"), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("token-123"));
    }

    [Test]
    public async Task Blad_PrzekazujeBladBramy()
    {
        var error = new Error("Auth.InvalidCredentials", ErrorType.Unauthorized);
        _auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(error));

        var result = await _handler.HandleAsync(new SignIn("jan@firma.pl", "0000"), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(error));
    }

    [Test]
    public async Task PrzekazujeTokenAnulowania()
    {
        using var cts = new CancellationTokenSource();

        await _handler.HandleAsync(new SignIn("jan@firma.pl", "1234"), cts.Token);

        _auth.Verify(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), cts.Token), Times.Once);
    }
}
