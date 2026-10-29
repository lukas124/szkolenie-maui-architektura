using FieldService.Domain.Common;

namespace FieldService.Domain.Visits;

public enum VisitOutcomeType
{
    Fixed,
    NeedsFollowUp,
    NotFixed
}

// Obiekt wartości: wynik wizyty. „Potrzebna kolejna wizyta” i „nie naprawiono” wymagają powodu.
public sealed record VisitOutcome
{
    private VisitOutcome(VisitOutcomeType type, string? reason)
    {
        Type = type;
        Reason = reason;
    }

    public VisitOutcomeType Type { get; }
    public string? Reason { get; }

    public static VisitOutcome Fixed { get; } = new(VisitOutcomeType.Fixed, null);

    public static Result<VisitOutcome> Create(VisitOutcomeType type, string? reason)
    {
        if (type == VisitOutcomeType.Fixed) return Result<VisitOutcome>.Success(Fixed);
        return string.IsNullOrWhiteSpace(reason)
            ? VisitErrors.ReasonRequired
            : Result<VisitOutcome>.Success(new VisitOutcome(type, reason.Trim()));
    }
}
