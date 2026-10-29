namespace FieldService.UI.Helpers;

// Dostęp do usług z kontenera z dowolnego miejsca aplikacji
public static class ServiceHelper
{
    public static T GetService<T>() where T : notnull =>
        IPlatformApplication.Current!.Services.GetRequiredService<T>();
}
