using FieldService.Application.Abstractions;

namespace FieldService.Infrastructure.Http;

// Liczniki żądań HTTP do pomiarów „przed / po” (singleton; liczy RequestCounterHandler)
public sealed class RequestStatistics : IRequestStatistics
{
    private readonly Lock _gate = new();
    private int _total, _ok, _notModified;
    private long _bytes;

    public void Add(int status, long bytes)
    {
        lock (_gate)
        {
            _total++;
            if (status == 200) _ok++;
            if (status == 304) _notModified++;
            _bytes += bytes;
        }
    }

    public RequestStats Snapshot()
    {
        lock (_gate) return new RequestStats(_total, _ok, _notModified, _bytes);
    }

    public void Reset()
    {
        lock (_gate)
        {
            _total = _ok = _notModified = 0;
            _bytes = 0;
        }
    }
}
