using System.Runtime.CompilerServices;
using FieldService.Presentation.Common;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Common;

// Typy testowe – unikalne dla tego pliku, więc liczniki nie mieszają się z innymi testami
internal sealed class TrackedProbe;

internal sealed class UntrackedProbe;

// ZAŁOŻENIE: BaseViewModel ma konstruktor bez parametrów i nie jest sealed
internal sealed class ProbeViewModel : BaseViewModel;

// ViewModelTracker: słabe referencje do żywych ViewModeli (Diagnostyka)
[TestFixture]
public sealed class ViewModelTrackerTests
{
    private readonly List<object> _keepAlive = [];

    [TearDown]
    public void TearDown()
    {
        _keepAlive.Clear();
        FullGc();
    }

    private static void FullGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackGarbage(int count)
    {
        for (var i = 0; i < count; i++)
        {
            ViewModelTracker.Track(new TrackedProbe());
        }
    }

    // Śledzone i osiągalne instancje są liczone
    [Test]
    public void CountAlive_ThreeTrackedAndReferenced_ReturnsThree()
    {
        for (var i = 0; i < 3; i++)
        {
            var probe = new TrackedProbe();
            _keepAlive.Add(probe);
            ViewModelTracker.Track(probe);
        }

        Assert.That(ViewModelTracker.CountAlive(typeof(TrackedProbe)), Is.EqualTo(3));
    }

    // Tracker trzyma słabo – nieosiągalne instancje po pełnym GC nie są liczone
    [Test]
    public void CountAlive_TrackedButUnreachable_ReturnsZeroAfterGc()
    {
        TrackGarbage(5);

        FullGc();

        Assert.That(ViewModelTracker.CountAlive(typeof(TrackedProbe)), Is.EqualTo(0));
    }

    // Typ, którego nikt nie śledził → 0
    [Test]
    public void CountAlive_TypeNeverTracked_ReturnsZero()
    {
        _keepAlive.Add(new UntrackedProbe());

        Assert.That(ViewModelTracker.CountAlive(typeof(UntrackedProbe)), Is.EqualTo(0));
    }

    // BaseViewModel rejestruje każdą instancję sam (bez jawnego Track)
    [Test]
    public void CountAlive_BaseViewModelCreated_CountsInstance()
    {
        _keepAlive.Add(new ProbeViewModel());
        _keepAlive.Add(new ProbeViewModel());

        Assert.That(ViewModelTracker.CountAlive(typeof(ProbeViewModel)), Is.EqualTo(2));
    }
}
