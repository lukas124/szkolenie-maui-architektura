namespace FieldService.Application.Abstractions;

// Port: bieżący czas (w testach stały, w aplikacji zegar systemowy)
public interface IClock
{
    DateTimeOffset Now { get; }
}
