using FieldService.Presentation.Visits;
using static FieldService.Presentation.Tests.BlackBox.SpecVisitData;

namespace FieldService.Presentation.Tests.BlackBox;

/// <summary>Szczegóły wizyty: parametr nawigacji (element listy) trafia do właściwości Visit.</summary>
[TestFixture]
public class VisitDetailsViewModelSpecTests
{
    [Test]
    public void PrzedNawigacja_BrakWizyty()
    {
        var vm = new VisitDetailsViewModel();

        Assert.That(vm.Visit, Is.Null);
    }

    [Test]
    public async Task OnNavigatedTo_UstawiaWizyteZParametru()
    {
        var vm = new VisitDetailsViewModel();
        var item = Item(NewVisit(At(12, 10)));

        await vm.OnNavigatedToAsync(item);

        Assert.That(vm.Visit, Is.SameAs(item));
    }

    [Test]
    public async Task OnNavigatedTo_PowiadamiaWidokOZmianieVisit()
    {
        var vm = new VisitDetailsViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await vm.OnNavigatedToAsync(Item(NewVisit(At(12, 10))));

        Assert.That(changed, Does.Contain(nameof(VisitDetailsViewModel.Visit)));
    }
}
