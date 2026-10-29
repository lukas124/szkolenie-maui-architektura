using FieldService.Domain.Common;

namespace FieldService.Application.Visits;

// Odmowy reguł aplikacyjnych (kto może, kiedy) – reguły domenowe są w VisitErrors
public static class VisitApplicationErrors
{
    // technik jest w jednym miejscu: nowa wizyta dopiero po zakończeniu tej w toku
    public static readonly Error AnotherInProgress = new("Visit.AnotherInProgress", ErrorType.Conflict);
}
