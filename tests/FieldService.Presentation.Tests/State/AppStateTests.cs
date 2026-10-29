using System.ComponentModel;
using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Presentation.State;

namespace FieldService.Presentation.Tests.State;

// Stan aplikacji: filtr listy wizyt zapamiętany w IPreferencesStore
[TestFixture]
public class AppStateTests
{
    private const string OverdueKey = "visits_filter_overdue";
    private const string StatusKey = "visits_filter_status";

    private Mock<IPreferencesStore> _preferences = null!;

    [SetUp]
    public void SetUp() => _preferences = new Mock<IPreferencesStore>();

    [Test]
    public void Constructor_SavedFilter_ReadsOnlyOverdueAndStatus()
    {
        _preferences.Setup(p => p.Get(OverdueKey)).Returns("true");
        _preferences.Setup(p => p.Get(StatusKey)).Returns("Planned");

        var appState = new AppState(_preferences.Object);

        Assert.Multiple(() =>
        {
            Assert.That(appState.Filter.OnlyOverdue, Is.True);
            Assert.That(appState.Filter.Status, Is.EqualTo(VisitStatus.Planned));
        });
    }

    [Test]
    public void Constructor_NothingSaved_FilterWithoutRestrictions()
    {
        _preferences.Setup(p => p.Get(It.IsAny<string>())).Returns((string?)null);

        var appState = new AppState(_preferences.Object);

        Assert.Multiple(() =>
        {
            Assert.That(appState.Filter.OnlyOverdue, Is.False);
            Assert.That(appState.Filter.Status, Is.Null);
        });
    }

    [Test]
    public void Constructor_SavedFalseAndEmptyStatus_FilterWithoutRestrictions()
    {
        _preferences.Setup(p => p.Get(OverdueKey)).Returns("false");
        _preferences.Setup(p => p.Get(StatusKey)).Returns("");

        var appState = new AppState(_preferences.Object);

        Assert.Multiple(() =>
        {
            Assert.That(appState.Filter.OnlyOverdue, Is.False);
            Assert.That(appState.Filter.Status, Is.Null);
        });
    }

    [Test]
    public void ApplyFilter_NewValues_ChangesFilter()
    {
        var appState = new AppState(_preferences.Object);

        appState.ApplyFilter(true, VisitStatus.Completed);

        Assert.Multiple(() =>
        {
            Assert.That(appState.Filter.OnlyOverdue, Is.True);
            Assert.That(appState.Filter.Status, Is.EqualTo(VisitStatus.Completed));
        });
    }

    [Test]
    public void ApplyFilter_NewValues_SavesPreferences()
    {
        var appState = new AppState(_preferences.Object);

        appState.ApplyFilter(true, VisitStatus.Completed);

        _preferences.Verify(p => p.Set(OverdueKey, "true"), Times.Once);
        _preferences.Verify(p => p.Set(StatusKey, "Completed"), Times.Once);
    }

    [Test]
    public void ApplyFilter_NoRestrictions_SavesFalseAndEmptyStatus()
    {
        _preferences.Setup(p => p.Get(OverdueKey)).Returns("true");
        _preferences.Setup(p => p.Get(StatusKey)).Returns("Planned");
        var appState = new AppState(_preferences.Object);

        appState.ApplyFilter(false, null);

        _preferences.Verify(p => p.Set(OverdueKey, "false"), Times.Once);
        _preferences.Verify(p => p.Set(StatusKey, ""), Times.Once);
        Assert.Multiple(() =>
        {
            Assert.That(appState.Filter.OnlyOverdue, Is.False);
            Assert.That(appState.Filter.Status, Is.Null);
        });
    }

    [Test]
    public void ApplyFilter_SavedValuesReadBack_NewInstanceHasSameFilter()
    {
        // Filtr przeżywa ubicie procesu: nowy AppState odczytuje to, co zapisał poprzedni
        var saved = new Dictionary<string, string>();
        _preferences.Setup(p => p.Set(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string key, string value) => saved[key] = value);
        _preferences.Setup(p => p.Get(It.IsAny<string>()))
            .Returns((string key) => saved.TryGetValue(key, out var value) ? value : null);

        new AppState(_preferences.Object).ApplyFilter(true, VisitStatus.InProgress);
        var restored = new AppState(_preferences.Object);

        Assert.Multiple(() =>
        {
            Assert.That(restored.Filter.OnlyOverdue, Is.True);
            Assert.That(restored.Filter.Status, Is.EqualTo(VisitStatus.InProgress));
        });
    }

    [Test]
    public void ApplyFilter_ChangedValues_RaisesPropertyChangedOnFilter()
    {
        var appState = new AppState(_preferences.Object);
        var changed = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => changed.Add(e.PropertyName);
        appState.Filter.PropertyChanged += handler;

        appState.ApplyFilter(true, VisitStatus.Cancelled);

        appState.Filter.PropertyChanged -= handler;
        Assert.That(changed, Is.SupersetOf(new[] { nameof(VisitFilterState.OnlyOverdue), nameof(VisitFilterState.Status) }));
    }

    [Test]
    public void ApplyFilter_SameFilterInstance_FilterObjectNotReplaced()
    {
        // Filter to jeden obserwowalny obiekt – ApplyFilter zmienia jego pola, a nie podmienia go
        var appState = new AppState(_preferences.Object);
        var filter = appState.Filter;

        appState.ApplyFilter(true, null);

        Assert.That(appState.Filter, Is.SameAs(filter));
    }
}
