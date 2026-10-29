using FieldService.Application.Abstractions;

namespace FieldService.Infrastructure;

// Adapter portu IClock: zegar systemowy
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
