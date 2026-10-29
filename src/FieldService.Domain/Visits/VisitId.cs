namespace FieldService.Domain.Visits;

// Tożsamość wizyty – osobny typ zamiast gołego Guid (nie da się pomylić z Id zlecenia)
public readonly record struct VisitId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
