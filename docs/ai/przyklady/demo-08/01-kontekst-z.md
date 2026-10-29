Nagrana odpowiedź LLM do dema (plan B) – do weryfikacji, nie wzorzec.

# Demo 08 · krok 1 – to samo pytanie z `AGENTS.md`

Repozytorium z `AGENTS.md`, `src/FieldService.Domain/AGENTS.md` i `docs/areas/visits.md`.

## Prompt

```text
Dodaj regułę: wynik NotFixed wymaga co najmniej jednej zapisanej czynności. Gdzie i jak?
```

## Odpowiedź LLM

To reguła domenowa wizyty (dotyczy zakresu i wyniku), więc według `AGENTS.md` należy do agregatu `Visit`, obok R5
w `Visit.Complete`. ViewModel niczego nie sprawdza – pokazuje błąd z `Result` przez `IUserFeedback`.

**1. Test (given–when–then) w `tests/FieldService.Domain.Tests/Visits/VisitTests.cs`:**

```csharp
[Test]
public void Complete_NotFixedWithoutActivities_FailsWithActivityRequiredAndChangesNothing()
{
    var visit = GivenInProgress();
    var outcome = VisitOutcome.Create(VisitOutcomeType.NotFixed, "Brak części").Value;
    var before = Capture(visit);

    var result = visit.Complete(outcome, Now);

    AssertFailedWithoutChange(result, VisitErrors.ActivityRequired, visit, before);
}
```

**2. Kod błędu w `src/FieldService.Domain/Visits/VisitErrors.cs`:**

```csharp
public static readonly Error ActivityRequired = new("Visit.ActivityRequired", ErrorType.Validation);
```

**3. Reguła w `Visit.Complete`, po sprawdzeniu statusu:**

```csharp
if (outcome.Type == VisitOutcomeType.NotFixed && Scope.Activities.Count == 0) return VisitErrors.ActivityRequired;
```

**4. Komunikat w `src/FieldService.UI/Resources/Strings/Strings.resx`:**

```xml
<data name="Error_Visit.ActivityRequired" xml:space="preserve">
  <value>Zapisz co najmniej jedną czynność, zanim zakończysz wizytę jako „nie naprawiono”.</value>
</data>
```

`ErrorMessageProvider` znajdzie tekst po kluczu `Error_{Code}` – ViewModel bez zmian. Sprawdzenie:
`dotnet test` (wszystkie projekty).
