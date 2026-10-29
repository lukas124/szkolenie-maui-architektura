# Ćwiczenie 08 · krok 2 – odpowiedź LLM: testy R7

Wariant B (bez dostępu do LLM): to jest odpowiedź na prompt z `docs/ai/prompty/01-test-najpierw.md`.
Przejrzyj ją tak, jak przeglądasz każdy kod od LLM – regułą R7 z `docs/areas/visits.md`
i listą `docs/ai/review-kodu.md`. Popraw to, co trzeba, zanim przeniesiesz testy do repozytorium.

## Prompt

```text
Napisz WYŁĄCZNIE testy NUnit dla nowej reguły modelu, bez implementacji.

Reguła R7: RemoveUsedPart(partId, now) usuwa omyłkowo dopisaną część z zakresu wizyty – tylko w InProgress
(jak R2); część musi być w zakresie → VisitErrors.PartNotInScope; po podpisie zmiana zakresu unieważnia
podpis (jak R3).
Metoda: Visit.RemoveUsedPart(PartId partId, DateTimeOffset now). Zwraca Result, nie rzuca wyjątków.
Przypadki (co najmniej): usuwa część w InProgress; po podpisie unieważnia podpis; część spoza zakresu →
PartNotInScope; zły status → NotInProgress.

Zasady: dopisz testy do tests/FieldService.Domain.Tests/Visits/VisitTests.cs, nazwy Metoda_Warunek_Wynik,
given–when–then, istniejące pomocniki Given…, Capture, AssertFailedWithoutChange.
```

## Odpowiedź LLM

Oto testy reguły R7. Dodałem dwa pomocniki `given` (wizyta z częścią w zakresie, przed podpisem i po nim) –
wklej je do sekcji `// ---------- given ----------`, a testy przed nią.

```csharp
    // ---------- R7: RemoveUsedPart ----------

    [Test]
    public void RemoveUsedPart_InProgress_RemovesPartAndRaisesPartRemoved()
    {
        var visit = GivenWithPart();
        var at = DateTime.Now;

        var result = visit.RemoveUsedPart(Filter, at);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Scope.Parts, Is.Empty);
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[] { new PartRemoved(visit.Id, Filter, at) }));
        });
    }

    [Test]
    public void RemoveUsedPart_AfterSignature_InvalidatesSignature()
    {
        var visit = GivenSignedWithPart();
        var scopeVersionBefore = visit.ScopeVersion;
        var at = Now.AddMinutes(40);

        var result = visit.RemoveUsedPart(Filter, at);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.HasValidSignature(), Is.False);
            Assert.That(visit.ScopeVersion, Is.EqualTo(scopeVersionBefore + 1));
            Assert.That(visit.DomainEvents, Is.EqualTo(new IDomainEvent[]
            {
                new PartRemoved(visit.Id, Filter, at),
                new CustomerSignatureInvalidated(visit.Id, at),
            }));
        });
    }

    [Test]
    public void RemoveUsedPart_PartNotInScope_FailsWithPartNotInScopeAndChangesNothing()
    {
        var visit = GivenWithPart();
        var before = Capture(visit);

        var result = visit.RemoveUsedPart(Valve, Now.AddMinutes(10));

        AssertFailedWithoutChange(result, VisitErrors.PartNotInScope, visit, before);
    }

    [Test]
    public void RemoveUsedPart_OnPlanned_FailsWithNotInProgressAndChangesNothing()
    {
        var visit = GivenPlanned();
        var before = Capture(visit);

        var result = visit.RemoveUsedPart(Filter, Now.AddMinutes(10));

        AssertFailedWithoutChange(result, VisitErrors.NotInProgress, visit, before);
    }

    [Test]
    public void RemoveUsedPart_OnCompleted_RemovesPart()
    {
        var visit = GivenSignedWithPart();
        Given(visit.Complete(VisitOutcome.Fixed, Now.AddMinutes(-5)));
        visit.ClearDomainEvents();

        var result = visit.RemoveUsedPart(Filter, Now.AddMinutes(10));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(visit.Scope.Parts, Is.Empty);
        });
    }

    // ---------- given (R7) ----------

    private static Visit GivenWithPart()
    {
        var visit = GivenInProgress();
        Given(visit.UsePart(Filter, Qty(2), Now.AddMinutes(-20)));
        visit.ClearDomainEvents();
        return visit;
    }

    private static Visit GivenSignedWithPart()
    {
        var visit = GivenWithPart();
        Given(visit.SignByCustomer(Signer, Now.AddMinutes(-10)));
        visit.ClearDomainEvents();
        return visit;
    }
```

Po dodaniu testów `dotnet test tests/FieldService.Domain.Tests` się nie skompiluje, bo `Visit.RemoveUsedPart`
jeszcze nie istnieje – to oczekiwany „czerwony” krok. Implementację przygotuję w następnym kroku.
