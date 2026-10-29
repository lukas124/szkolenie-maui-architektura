namespace FieldService.Domain.Common;

// Rodzaj błędu – po nim aplikacja decyduje, co zrobić (np. Network = pokaż dane offline, Unauthorized = logowanie)
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Network,
    Unexpected
}

// Błąd z kodem (np. "Visits.Network"). Tekst dla człowieka wybiera prezentacja na podstawie kodu.
public sealed record Error(string Code, ErrorType Type);

// Wynik operacji: sukces albo przewidywalny błąd – bez wyjątków przez granice pierścieni
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Brak wartości: wynik jest błędem.");

    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(Error error) => new(error);
}
