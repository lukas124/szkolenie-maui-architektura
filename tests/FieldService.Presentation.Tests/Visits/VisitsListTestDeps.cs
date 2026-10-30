using FieldService.Presentation.Visits;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FieldService.Presentation.Tests.Visits;

// Zależności listy wizyt, które nie są przedmiotem testu (log pomiaru)
internal static class VisitsListTestDeps
{
    public static ILogger<VisitsListViewModel> Logger() => NullLogger<VisitsListViewModel>.Instance;
}
