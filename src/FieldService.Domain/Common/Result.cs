namespace FieldService.Domain.Common;

// Wynik operacji: sukces albo przewidywalny błąd (naruszona reguła, brak sieci) – bez wyjątków przez granice pierścieni.
// Wyjątek zostaje dla błędu programisty.
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Brak wartości: wynik jest błędem.");

    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
