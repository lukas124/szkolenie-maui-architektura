using FieldService.Application.Abstractions;

namespace FieldService.TestDoubles;

// Zegar o stałym, ustawialnym czasie – testy nie zależą od DateTime.Now.
public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public static readonly DateTimeOffset DefaultNow = new(2026, 10, 29, 9, 0, 0, TimeSpan.FromHours(1));

    public FixedClock() : this(DefaultNow)
    {
    }

    public DateTimeOffset Now { get; set; } = now;

    public void Advance(TimeSpan by) => Now += by;
}
