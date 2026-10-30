namespace FieldService.Application.Abstractions;

// Liczniki żądań HTTP do pomiarów „przed / po” (Diagnostyka; liczone tylko w Debug i Test)
public sealed record RequestStats(int Total, int Ok, int NotModified, long Bytes);

public interface IRequestStatistics
{
    RequestStats Snapshot();

    void Reset();
}
