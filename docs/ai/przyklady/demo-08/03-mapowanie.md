Nagrana odpowiedź LLM do dema (plan B) – do weryfikacji, nie wzorzec.

# Demo 08 · krok 3 – mapowanie `VisitDtoMapper.ToSummary` z testem nieznanego statusu

To propozycja LLM z dema, a nie kod checkpointu. W checkpoincie `VisitDtoMapper.ToStatus` używa
`Enum.TryParse` i nieznany status zamienia na `VisitStatus.Planned`; typu `VisitStatus.Unknown` w kodzie nie ma.

## Prompt

```text
VisitDtoMapper.ToSummary + testy, także dla nieznanego statusu z API.
```

## ✗ Pierwsza odpowiedź LLM

```csharp
private static VisitStatus ToStatus(string status) => Enum.Parse<VisitStatus>(status, ignoreCase: true);
```

```csharp
[Test]
public void ToSummary_UnknownStatus_MapsToUnknown()
{
    var dto = SampleDto(status: "Rescheduled");

    var summary = VisitDtoMapper.ToSummary(dto);

    Assert.That(summary.Status, Is.EqualTo(VisitStatus.Unknown));
}
```

Wynik `dotnet test`: test czerwony – `Enum.Parse` rzuca `ArgumentException` dla `"Rescheduled"`.

## Prompt po czerwonym teście

```text
Test ToSummary_UnknownStatus_MapsToUnknown jest czerwony – popraw implementację, nie test.
```

## ✓ Odpowiedź LLM po poprawce

```csharp
// VisitStatus.cs – nowa wartość na status, którego aplikacja nie zna
public enum VisitStatus { Unknown, Planned, InProgress, Completed, Cancelled }

// VisitDtoMapper.cs
private static VisitStatus ToStatus(string status) =>
    Enum.TryParse<VisitStatus>(status, ignoreCase: true, out var value) ? value : VisitStatus.Unknown;
```

Test zielony, test bez zmian (`git diff --stat tests/`). Przy przeglądzie: nowa wartość enuma dotyka `VisitFormatter`
i `Strings.resx` (tekst statusu) – to osobny, mały krok.
