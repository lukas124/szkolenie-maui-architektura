using FieldService.Domain.Common;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>
/// Fasada IUserFeedback: składa tytuł, tekst i przyciski z lokalizatora i dostawcy komunikatów,
/// a pokazuje przez port IDialogService.
/// </summary>
[TestFixture]
public class UserFeedbackSpecTests
{
    private const string TitleKey = "Visit_Cancel_Title";
    private const string MessageKey = "Visit_Cancel_Message";
    private const string TitleText = "Anulować wizytę?";
    private const string MessageText = "Zakres prac zostanie odrzucony.";

    private Mock<IDialogService> _dialogs = null!;
    private Mock<IErrorMessageProvider> _messages = null!;
    private UserFeedback _feedback = null!;

    [SetUp]
    public void SetUp()
    {
        _dialogs = new Mock<IDialogService>();
        _dialogs.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _messages = new Mock<IErrorMessageProvider>();
        var localizer = new SpecLocalizer(new Dictionary<string, string>
        {
            [TitleKey] = TitleText,
            [MessageKey] = MessageText,
        });
        _feedback = new UserFeedback(_dialogs.Object, _messages.Object, localizer);
    }

    [Test]
    public async Task ShowError_PokazujeAlertZTytulemTekstemIPrzyciskiem()
    {
        var error = new Error("Network.Unavailable", ErrorType.Network);
        _messages.Setup(m => m.GetMessage(error)).Returns("Brak sieci – spróbuj później.");

        await _feedback.ShowErrorAsync(error);

        _dialogs.Verify(d => d.ShowAlertAsync(SpecLocalizer.ErrorTitle, "Brak sieci – spróbuj później.", SpecLocalizer.Ok), Times.Once);
        _dialogs.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShowError_TekstPochodziZDostawcyKomunikatow()
    {
        var error = new Error("Visit.SignatureRequired", ErrorType.Validation);
        _messages.Setup(m => m.GetMessage(It.IsAny<Error>())).Returns("tekst");

        await _feedback.ShowErrorAsync(error);

        _messages.Verify(m => m.GetMessage(error), Times.Once);
    }

    // Wygasła sesja: komunikat pokazuje ekran logowania (ISessionExpiry), nie każde żądanie z 401
    [Test]
    public async Task ShowError_Unauthorized_ShowsNothing()
    {
        await _feedback.ShowErrorAsync(new Error("Api.Unauthorized", ErrorType.Unauthorized));

        _dialogs.VerifyNoOtherCalls();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Confirm_PytaZTekstamiZKluczyIZwracaOdpowiedz(bool answer)
    {
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(answer);

        var result = await _feedback.ConfirmAsync(TitleKey, MessageKey);

        Assert.That(result, Is.EqualTo(answer));
        _dialogs.Verify(d => d.ConfirmAsync(TitleText, MessageText, SpecLocalizer.Yes, SpecLocalizer.No), Times.Once);
    }
}
