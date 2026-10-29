using FieldService.Domain.Common;

namespace FieldService.Domain.Visits;

// Odmowy reguł wizyty (kod → tekst w Strings.resx: Error_{Code})
public static class VisitErrors
{
    public static readonly Error NotFound = new("Visit.NotFound", ErrorType.NotFound);
    public static readonly Error InvalidTransition = new("Visit.InvalidTransition", ErrorType.Conflict);
    public static readonly Error NotInProgress = new("Visit.NotInProgress", ErrorType.Conflict);
    public static readonly Error AlreadyCompleted = new("Visit.AlreadyCompleted", ErrorType.Conflict);
    public static readonly Error VersionConflict = new("Visit.VersionConflict", ErrorType.Conflict);
    public static readonly Error EmptyScope = new("Visit.EmptyScope", ErrorType.Validation);
    public static readonly Error SignatureRequired = new("Visit.SignatureRequired", ErrorType.Validation);
    public static readonly Error ReasonRequired = new("Visit.ReasonRequired", ErrorType.Validation);
    public static readonly Error SignerRequired = new("Visit.SignerRequired", ErrorType.Validation);
    public static readonly Error InvalidQuantity = new("Visit.InvalidQuantity", ErrorType.Validation);
    public static readonly Error ActivityCodeRequired = new("Visit.ActivityCodeRequired", ErrorType.Validation);
}
