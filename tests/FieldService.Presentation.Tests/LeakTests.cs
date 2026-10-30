using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Messaging;
using FieldService.Presentation.Tests.Visits;
using FieldService.Presentation.Tests.Visits.Completion;
using NUnit.Framework;

namespace FieldService.Presentation.Tests;

// Testy wycieków: ViewModel po zamknięciu nie może być osiągalny (posłaniec ani zdarzenie go nie trzyma).
// Obiekt powstaje w osobnej metodzie NoInlining – w metodzie testu nie zostaje żadna silna referencja.
[TestFixture]
public sealed class LeakTests
{
    // atrapy żyją w polach testu (trzymają wywołania, ale nie ViewModel)
    private VisitDetailsFixture _details = null!;
    private CompleteVisitFlowFixture _flow = null!;

    [SetUp]
    public void SetUp()
    {
        _details = new VisitDetailsFixture();
        _flow = new CompleteVisitFlowFixture();
    }

    [TearDown]
    public void TearDown() => WeakReferenceMessenger.Default.Reset();

    // Pełne odśmiecanie: zbiórka, finalizatory, druga zbiórka (zbiera to, co zwolniły finalizatory)
    private static void FullGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // NoInlining: ViewModel żyje tylko w tej metodzie, po powrocie test nie ma do niego silnej referencji
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference OpenAndCloseVisitDetails()
    {
        // ViewModel z atrapami, jak przy otwarciu strony szczegółów
        var viewModel = _details.CreateViewModel();
        // wejście na stronę: OnNavigatedToAsync ładuje wizytę i zapisuje ViewModel na powiadomienia o wysyłce zmian
        VisitDetailsFixture.NavigateAsync(viewModel).GetAwaiter().GetResult();
        // wyjście ze strony: OnClosed() wypisuje ViewModel z powiadomień
        viewModel.OnClosed();
        // słaba referencja nie trzyma obiektu – po FullGc() IsAlive mówi, czy GC go zebrał
        return new WeakReference(viewModel);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference OpenAndCloseCompleteVisitFlow()
    {
        var viewModel = _flow.CreateViewModel();
        viewModel.OnClosed();
        return new WeakReference(viewModel);
    }

    // Szczegóły wizyty po OnNavigatedToAsync i OnClosed() są zbierane przez GC
    [Test]
    public void VisitDetailsViewModel_IsCollected_AfterClose()
    {
        var weak = OpenAndCloseVisitDetails();

        FullGc();

        // nikt już nie trzyma ViewModelu, więc GC musiał go zebrać
        Assert.That(weak.IsAlive, Is.False);
    }

    // Kreator zakończenia wizyty po OnClosed() jest zbierany przez GC
    [Test]
    public void CompleteVisitFlowViewModel_IsCollected_AfterClose()
    {
        var weak = OpenAndCloseCompleteVisitFlow();

        FullGc();

        Assert.That(weak.IsAlive, Is.False);
    }

    // Dziesięć wejść i wyjść ze szczegółów – żaden ViewModel nie zostaje w pamięci (scenariusz z demo)
    [Test]
    public void VisitDetailsViewModel_TenOpenAndClose_NoneAlive()
    {
        var weakReferences = Enumerable.Range(0, 10).Select(_ => OpenAndCloseVisitDetails()).ToList();

        FullGc();

        Assert.That(weakReferences.Count(w => w.IsAlive), Is.EqualTo(0));
    }
}
