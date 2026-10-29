using FieldService.Domain.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Tests.Feedback;

[TestFixture]
public class ErrorMessageProviderTests
{
    private readonly ErrorMessageProvider _provider = new(new TestLocalizer());

    [Test]
    public void GetMessage_KnownCode_UsesTextForCode() =>
        Assert.That(_provider.GetMessage(new Error("Auth.InvalidCredentials", ErrorType.Unauthorized)),
            Is.EqualTo("Nieprawidłowy e-mail lub PIN."));

    [Test]
    public void GetMessage_UnknownCode_FallsBackToErrorType() =>
        Assert.That(_provider.GetMessage(new Error("Visits.Network", ErrorType.Network)), Is.EqualTo("Brak połączenia z serwerem."));

    [Test]
    public void GetMessage_UnknownCodeAndType_UsesUnexpected() =>
        Assert.That(_provider.GetMessage(new Error("Visits.Strange", ErrorType.Conflict)), Is.EqualTo("Nieoczekiwany błąd."));
}
