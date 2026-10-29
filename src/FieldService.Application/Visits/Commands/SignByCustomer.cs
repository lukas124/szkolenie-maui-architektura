using FieldService.Application.Abstractions;
using FieldService.Domain.Common;
using FieldService.Domain.Visits;

namespace FieldService.Application.Visits.Commands;

public sealed record SignByCustomer(VisitId VisitId, string SignerName);

public sealed class SignByCustomerHandler(IVisitRepository visits, IClock clock)
{
    public async Task<Result> HandleAsync(SignByCustomer command, CancellationToken ct)
    {
        var visit = await visits.GetAsync(command.VisitId, ct);
        if (visit is null) return VisitErrors.NotFound;

        var signed = visit.SignByCustomer(command.SignerName, clock.Now);
        if (!signed.IsSuccess) return signed;

        return await visits.SaveAsync(visit, ct);
    }
}
