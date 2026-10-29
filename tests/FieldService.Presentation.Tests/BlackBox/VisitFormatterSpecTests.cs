using FieldService.Application.Abstractions;
using FieldService.Domain.Visits;
using FieldService.Presentation.Visits;
using static FieldService.Presentation.Tests.BlackBox.SpecVisitData;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>
/// Formatter tekstów wizyty (pl-PL): okno czasowe „dziś/jutro/dzień tygodnia”, „za N min”,
/// podsumowanie dnia i czas aktualizacji. Strefa technika = strefa czasowa z ILocalizer (tu Europe/Warsaw).
/// </summary>
[TestFixture]
public class VisitFormatterSpecTests
{
    private static VisitFormatter Formatter(DateTimeOffset now) =>
        new(new SpecLocalizer(), Mock.Of<IClock>(c => c.Now == now));

    // --- FormatTimeWindow ---

    [Test]
    public void OknoDzisiaj_DzisZGodzinami()
    {
        var text = Formatter(At(12, 7)).FormatTimeWindow(At(12, 10), At(12, 11, 30));

        Assert.That(text, Is.EqualTo("dziś 10:00–11:30"));
    }

    [Test]
    public void OknoJutro_JutroZGodzinamiBezZeraWiodacego()
    {
        var text = Formatter(At(12, 7)).FormatTimeWindow(At(13, 8), At(13, 9, 30));

        Assert.That(text, Is.EqualTo("jutro 8:00–9:30"));
    }

    [Test]
    public void OknoInnegoDnia_SkrotDniaIData()
    {
        var text = Formatter(At(12, 7)).FormatTimeWindow(At(14, 14), At(14, 15, 30));

        Assert.That(text, Is.EqualTo("sob. 14.11 14:00–15:30"));
    }

    [Test]
    public void DzisIJutro_LiczoneWzgledemDatyZZegara_NieZegaraSystemowego()
    {
        // Zegar „stoi” w 2026 r. – data systemowa nie może wpływać na wynik.
        var formatter = Formatter(At(13, 7));

        Assert.That(formatter.FormatTimeWindow(At(13, 10), At(13, 11)), Is.EqualTo("dziś 10:00–11:00"));
        Assert.That(formatter.FormatTimeWindow(At(14, 10), At(14, 11)), Is.EqualTo("jutro 10:00–11:00"));
    }

    [Test]
    public void CzasWUtc_PokazanyWStrefieTechnika()
    {
        // 09:00Z = 10:00 CET.
        var from = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.Zero);

        var text = Formatter(At(12, 7)).FormatTimeWindow(from, to);

        Assert.That(text, Is.EqualTo("dziś 10:00–11:30"));
    }

    [Test]
    public void CzasWUtc_PoPolnocyWStrefieTechnika_ToJutro()
    {
        // 23:30Z 12.11 = 0:30 CET 13.11 – dla technika to już jutro.
        var from = new DateTimeOffset(2026, 11, 12, 23, 30, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 11, 13, 0, 30, 0, TimeSpan.Zero);

        var text = Formatter(At(12, 20)).FormatTimeWindow(from, to);

        Assert.That(text, Is.EqualTo("jutro 0:30–1:30"));
    }

    [Test]
    public void StrefaTechnikaZUstawienTelefonu_InnaStrefaNizWarszawa()
    {
        // Telefon technika w strefie Nowego Jorku: 10:00 CET = 4:00 w strefie technika.
        var localizer = new SpecLocalizer { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York") };
        var now = At(12, 7);
        var formatter = new VisitFormatter(localizer, Mock.Of<IClock>(c => c.Now == now));

        var text = formatter.FormatTimeWindow(At(12, 10), At(12, 11));

        Assert.That(text, Is.EqualTo("dziś 4:00–5:00"));
    }

    // --- FormatStartsIn ---

    [Test]
    public void StartWCiaguGodziny_ZaNMin()
    {
        var text = Formatter(At(12, 10)).FormatStartsIn(At(12, 10, 25));

        Assert.That(text, Is.EqualTo("za 25 min"));
    }

    [Test]
    public void StartZa59Minut_ZaNMin()
    {
        var text = Formatter(At(12, 10)).FormatStartsIn(At(12, 10, 59));

        Assert.That(text, Is.EqualTo("za 59 min"));
    }

    [Test]
    public void StartWInnymOffsecie_LiczonyBezwzglednie()
    {
        // 09:20Z = 10:20 CET, „teraz” 10:00 CET.
        var text = Formatter(At(12, 10)).FormatStartsIn(new DateTimeOffset(2026, 11, 12, 9, 20, 0, TimeSpan.Zero));

        Assert.That(text, Is.EqualTo("za 20 min"));
    }

    [Test]
    public void StartPozniejNizZaGodzine_Null()
    {
        Assert.That(Formatter(At(12, 10)).FormatStartsIn(At(12, 11, 1)), Is.Null);
        Assert.That(Formatter(At(12, 10)).FormatStartsIn(At(12, 14)), Is.Null);
    }

    [Test]
    public void StartWPrzeszlosci_Null()
    {
        Assert.That(Formatter(At(12, 10)).FormatStartsIn(At(12, 9, 30)), Is.Null);
        Assert.That(Formatter(At(12, 10)).FormatStartsIn(At(12, 9, 59)), Is.Null);
    }

    // --- FormatDaySummary ---

    [Test]
    public void PodsumowanieDnia_ZakonczoneZWszystkichIDoZrobienia()
    {
        var visits = new[]
        {
            NewVisit(At(12, 8), VisitStatus.Completed),
            NewVisit(At(12, 9), VisitStatus.Completed),
            NewVisit(At(12, 10), VisitStatus.InProgress),
            NewVisit(At(12, 12)),
        };

        var text = Formatter(At(12, 10)).FormatDaySummary(visits);

        Assert.That(text, Is.EqualTo("Zakończone: 2 z 4 · do zrobienia: 2"));
    }

    [Test]
    public void PodsumowanieDnia_SpoznionaZaplanowanaJestDoZrobienia()
    {
        var visits = new[]
        {
            NewVisit(At(12, 8), VisitStatus.Completed),
            NewVisit(At(12, 9), isLate: true),
        };

        var text = Formatter(At(12, 10)).FormatDaySummary(visits);

        Assert.That(text, Is.EqualTo("Zakończone: 1 z 2 · do zrobienia: 1"));
    }

    [Test]
    public void PodsumowanieDnia_BrakWizyt()
    {
        var text = Formatter(At(12, 10)).FormatDaySummary([]);

        Assert.That(text, Is.EqualTo("Brak wizyt na dziś"));
    }

    // --- FormatUpdatedNow ---

    [Test]
    public void Aktualizacja_GodzinaZZegara()
    {
        var text = Formatter(At(12, 10, 15)).FormatUpdatedNow();

        Assert.That(text, Does.StartWith("Aktualizacja: "));
        Assert.That(text, Does.Contain("10:15"));
    }
}
