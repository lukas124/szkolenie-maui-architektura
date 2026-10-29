using System.Globalization;
using System.Text.Json;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;
using FieldService.Infrastructure.Outbox;
using FieldService.TestDoubles;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Outbox;

// Zdarzenie domenowe wizyty → wiadomość outbox (Type, VisitId, Payload, CreatedAt).
// Zdarzenia tworzymy metodami Visit, a wybieramy po nazwie typu – testy nie zależą od konstruktorów zdarzeń.
[TestFixture]
public class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = FixedClock.DefaultNow.AddMinutes(30);

    private static VisitId NewId() => new(Guid.NewGuid());

    private static IDomainEvent EventNamed(Visit visit, string typeName) =>
        visit.DomainEvents.Single(e => e.GetType().Name == typeName);

    private static JsonElement PayloadOf(OutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        return document.RootElement.Clone();
    }

    [Test]
    public void From_VisitStarted_CreatesMessageWithStartedAt()
    {
        var id = NewId();
        var visit = TestVisits.Planned(id);
        visit.Start(Now);

        var message = OutboxMessage.From(EventNamed(visit, "VisitStarted"));

        Assert.That(message, Is.Not.Null);
        Assert.That(message!.Type, Is.EqualTo("VisitStarted"));
        Assert.That(message.VisitId, Is.EqualTo(id.Value.ToString("D")));
        var payload = PayloadOf(message);
        Assert.That(payload.GetProperty("startedAt").GetDateTimeOffset(), Is.EqualTo(Now));
    }

    [Test]
    public void From_ActivityRecorded_CreatesMessageWithCodeDescriptionAndTime()
    {
        var id = NewId();
        var visit = TestVisits.InProgress(id);
        visit.RecordActivity("DIAG", "Diagnostyka", Now);

        var message = OutboxMessage.From(EventNamed(visit, "ActivityRecorded"));

        Assert.That(message, Is.Not.Null);
        Assert.That(message!.Type, Is.EqualTo("ActivityRecorded"));
        Assert.That(message.VisitId, Is.EqualTo(id.Value.ToString("D")));
        var payload = PayloadOf(message);
        Assert.That(payload.GetProperty("code").GetString(), Is.EqualTo("DIAG"));
        Assert.That(payload.GetProperty("description").GetString(), Is.EqualTo("Diagnostyka"));
        Assert.That(payload.GetProperty("recordedAt").GetDateTimeOffset(), Is.EqualTo(Now));
    }

    [Test]
    public void From_PartUsed_CreatesMessageWithPartAndQuantityAsNumber()
    {
        var id = NewId();
        var visit = TestVisits.InProgress(id);
        visit.UsePart(TestVisits.Filter, Quantity.Create(2).Value, Now);

        var message = OutboxMessage.From(EventNamed(visit, "PartUsed"));

        Assert.That(message, Is.Not.Null);
        Assert.That(message!.Type, Is.EqualTo("PartUsed"));
        Assert.That(message.VisitId, Is.EqualTo(id.Value.ToString("D")));
        var payload = PayloadOf(message);
        Assert.That(payload.GetProperty("partId").GetString(), Is.EqualTo(TestVisits.Filter.Value));
        Assert.That(payload.GetProperty("quantity").ValueKind, Is.EqualTo(JsonValueKind.Number));
        Assert.That(payload.GetProperty("quantity").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public void From_PartRemoved_CreatesMessageWithPart()
    {
        var id = NewId();
        var visit = TestVisits.InProgress(id);
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, Now);
        visit.ClearDomainEvents();
        visit.RemoveUsedPart(TestVisits.Filter, Now.AddMinutes(1));

        var message = OutboxMessage.From(EventNamed(visit, "PartRemoved"));

        Assert.That(message, Is.Not.Null);
        Assert.That(message!.Type, Is.EqualTo("PartRemoved"));
        Assert.That(message.VisitId, Is.EqualTo(id.Value.ToString("D")));
        var payload = PayloadOf(message);
        Assert.That(payload.GetProperty("partId").GetString(), Is.EqualTo(TestVisits.Filter.Value));
    }

    [Test]
    public void From_CustomerSigned_CreatesMessageWithSignerAndSignedAt()
    {
        var id = NewId();
        var visit = TestVisits.WithScope(id);
        visit.SignByCustomer("Anna Nowak", Now);

        var message = OutboxMessage.From(EventNamed(visit, "CustomerSigned"));

        Assert.That(message, Is.Not.Null);
        Assert.That(message!.Type, Is.EqualTo("CustomerSigned"));
        Assert.That(message.VisitId, Is.EqualTo(id.Value.ToString("D")));
        var payload = PayloadOf(message);
        Assert.That(payload.GetProperty("signerName").GetString(), Is.EqualTo("Anna Nowak"));
        Assert.That(payload.GetProperty("signedAt").GetDateTimeOffset(), Is.EqualTo(Now));
    }

    [Test]
    public void From_VisitCompletedFixed_CreatesMessageWithOutcomeAndEmptyReason()
    {
        var id = NewId();
        var visit = TestVisits.Signed(id);
        visit.Complete(VisitOutcome.Create(VisitOutcomeType.Fixed, null).Value, Now);

        var message = OutboxMessage.From(EventNamed(visit, "VisitCompleted"));

        Assert.That(message, Is.Not.Null);
        Assert.That(message!.Type, Is.EqualTo("VisitCompleted"));
        Assert.That(message.VisitId, Is.EqualTo(id.Value.ToString("D")));
        var payload = PayloadOf(message);
        Assert.That(payload.GetProperty("outcome").GetString(), Is.EqualTo("Fixed"));
        Assert.That(payload.GetProperty("reason").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(payload.GetProperty("completedAt").GetDateTimeOffset(), Is.EqualTo(Now));
    }

    [Test]
    public void From_VisitCompletedNotFixed_CreatesMessageWithReason()
    {
        var visit = TestVisits.Signed(NewId());
        visit.Complete(VisitOutcome.Create(VisitOutcomeType.NotFixed, "Brak części").Value, Now);

        var message = OutboxMessage.From(EventNamed(visit, "VisitCompleted"));

        var payload = PayloadOf(message!);
        Assert.That(payload.GetProperty("outcome").GetString(), Is.EqualTo("NotFixed"));
        Assert.That(payload.GetProperty("reason").GetString(), Is.EqualTo("Brak części"));
    }

    [Test]
    public void From_VisitCompletedNeedsFollowUp_SavesOutcomeAsText()
    {
        var visit = TestVisits.Signed(NewId());
        visit.Complete(VisitOutcome.Create(VisitOutcomeType.NeedsFollowUp, "Potrzebna druga wizyta").Value, Now);

        var message = OutboxMessage.From(EventNamed(visit, "VisitCompleted"));

        var payload = PayloadOf(message!);
        Assert.That(payload.GetProperty("outcome").GetString(), Is.EqualTo("NeedsFollowUp"));
    }

    [Test]
    public void From_CustomerSignatureInvalidated_ReturnsNull()
    {
        // zmiana zakresu po podpisie unieważnia podpis – serwer robi to sam, więc nie wysyłamy
        var visit = TestVisits.Signed(NewId());
        visit.UsePart(TestVisits.Filter, Quantity.Create(1).Value, Now);

        var message = OutboxMessage.From(EventNamed(visit, "CustomerSignatureInvalidated"));

        Assert.That(message, Is.Null);
    }

    [Test]
    public void From_VisitCancelled_ReturnsNull()
    {
        // anulowanie należy do dyspozytora – nie wysyłamy
        var visit = TestVisits.Planned(NewId());
        visit.Cancel("Klient odwołał", Now);

        var message = OutboxMessage.From(EventNamed(visit, "VisitCancelled"));

        Assert.That(message, Is.Null);
    }

    [Test]
    public void From_Event_SetsCreatedAtToEventTimeUtc()
    {
        var visit = TestVisits.Planned(NewId());
        visit.Start(Now);

        var message = OutboxMessage.From(EventNamed(visit, "VisitStarted"))!;

        var createdAt = DateTimeOffset.Parse(message.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.That(createdAt, Is.EqualTo(Now));
        Assert.That(createdAt.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void From_NewMessage_HasZeroAttemptsAndIsNotSent()
    {
        var visit = TestVisits.Planned(NewId());
        visit.Start(Now);

        var message = OutboxMessage.From(EventNamed(visit, "VisitStarted"))!;

        Assert.That(message.Attempts, Is.EqualTo(0));
        Assert.That(message.LastError, Is.Null);
        Assert.That(message.SentAt, Is.Null);
        Assert.That(message.Failed, Is.False);
    }

    [Test]
    public void From_EachCall_AssignsNewIdInGuidFormat()
    {
        var visit = TestVisits.Planned(NewId());
        visit.Start(Now);
        var domainEvent = EventNamed(visit, "VisitStarted");

        var first = OutboxMessage.From(domainEvent)!;
        var second = OutboxMessage.From(domainEvent)!;

        Assert.That(Guid.TryParseExact(first.Id, "D", out _), Is.True);
        Assert.That(Guid.TryParseExact(second.Id, "D", out _), Is.True);
        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
    }
}
