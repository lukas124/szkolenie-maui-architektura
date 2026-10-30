namespace FieldService.Application.Abstractions;

// Port: czy telefon ma teraz dostęp do internetu – stan sieci zna platforma
public interface INetworkStatus
{
    bool IsOnline { get; }
}
