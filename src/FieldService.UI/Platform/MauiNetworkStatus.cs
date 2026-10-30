using FieldService.Application.Abstractions;

namespace FieldService.UI.Platform;

// Adapter portu INetworkStatus: internet według systemu (Wi-Fi lub dane komórkowe z dostępem do sieci)
public sealed class MauiNetworkStatus : INetworkStatus
{
    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
}
