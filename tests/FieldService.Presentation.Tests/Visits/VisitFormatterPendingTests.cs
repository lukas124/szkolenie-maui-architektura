using FieldService.Application.Abstractions;
using System.Globalization;
using FieldService.Presentation.Common;
using FieldService.Presentation.Visits;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Visits;

// Tekst licznika „Oczekuje na wysłanie (N)” z zasobu Visits_PendingSync
[TestFixture]
public class VisitFormatterPendingTests
{
    private Mock<ILocalizer> _localizer = null!;

    [SetUp]
    public void SetUp()
    {
        // własna atrapa lokalizatora: zadany tekst dla Visits_PendingSync, dla innych kluczy sam klucz
        _localizer = new Mock<ILocalizer>();
        _localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        _localizer.Setup(l => l["Visits_PendingSync"]).Returns("Oczekuje na wysłanie ({0})");
        _localizer.Setup(l => l.CurrentCulture).Returns(CultureInfo.GetCultureInfo("pl-PL"));
    }

    private VisitFormatter CreateFormatter() => new(_localizer.Object, Mock.Of<IClock>());

    [Test]
    public void FormatPending_TwoMessages_InsertsNumberIntoText()
    {
        var text = CreateFormatter().FormatPending(2);

        Assert.That(text, Is.EqualTo("Oczekuje na wysłanie (2)"));
    }

    [Test]
    public void FormatPending_Zero_InsertsZero()
    {
        var text = CreateFormatter().FormatPending(0);

        Assert.That(text, Is.EqualTo("Oczekuje na wysłanie (0)"));
    }

    [Test]
    public void FormatPending_DifferentResourceText_UsesTextFromLocalizer()
    {
        _localizer.Setup(l => l["Visits_PendingSync"]).Returns("Pending upload: {0}");
        _localizer.Setup(l => l.CurrentCulture).Returns(CultureInfo.GetCultureInfo("en-US"));

        var text = CreateFormatter().FormatPending(7);

        Assert.That(text, Is.EqualTo("Pending upload: 7"));
    }

    [Test]
    public void FormatPending_Always_GetsTextFromVisitsPendingSyncKey()
    {
        CreateFormatter().FormatPending(1);

        _localizer.Verify(l => l["Visits_PendingSync"], Times.AtLeastOnce);
    }
}
