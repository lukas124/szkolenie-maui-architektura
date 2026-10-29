using FieldService.Application.Visits.Commands;
using FieldService.Domain.Visits;
using FieldService.TestDoubles;

namespace FieldService.Application.Tests.Visits;

// Przypadek użycia „Podpis klienta”: podpis bieżącej wersji zakresu z czasem z IClock; odmowa bez zapisu.
[TestFixture]
public class SignByCustomerHandlerTests
{
    private InMemoryVisitRepository _visits = null!;
    private FixedClock _clock = null!;
    private SignByCustomerHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _visits = new InMemoryVisitRepository();
        _clock = new FixedClock();
        _handler = new SignByCustomerHandler(_visits, _clock);
    }

    [Test]
    public async Task UnknownVisit_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(
            new SignByCustomer(new VisitId(Guid.NewGuid()), TestVisits.Signer), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(VisitErrors.NotFound));
    }

    [Test]
    public async Task WithScope_SavesSignatureForCurrentScope()
    {
        var visit = TestVisits.WithScope();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new SignByCustomer(visit.Id, TestVisits.Signer), CancellationToken.None);

        var stored = _visits.Stored(visit.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_visits.SaveCount, Is.EqualTo(1));
            Assert.That(stored.HasValidSignature(), Is.True);
            Assert.That(stored.Signature?.SignerName, Is.EqualTo(TestVisits.Signer));
            Assert.That(stored.Signature?.SignedAt, Is.EqualTo(_clock.Now));
            Assert.That(stored.Signature?.ScopeVersion, Is.EqualTo(stored.ScopeVersion));
        });
    }

    [Test]
    public async Task EmptyScope_ReturnsEmptyScopeAndDoesNotSave()
    {
        var visit = TestVisits.InProgress();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new SignByCustomer(visit.Id, TestVisits.Signer), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.EmptyScope));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task EmptySigner_ReturnsSignerRequiredAndDoesNotSave()
    {
        var visit = TestVisits.WithScope();
        _visits.Add(visit);

        var result = await _handler.HandleAsync(new SignByCustomer(visit.Id, ""), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(VisitErrors.SignerRequired));
            Assert.That(_visits.SaveCount, Is.EqualTo(0));
            Assert.That(_visits.Stored(visit.Id).Signature, Is.Null);
        });
    }
}
