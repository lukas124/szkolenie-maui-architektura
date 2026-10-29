using Microsoft.Extensions.DependencyInjection;

namespace FieldService.Presentation.Common;

// TODO: Ćwiczenie 09 – lokalizator usług (ang. Service Locator): ukryte zależności, kontener poza korzeniem kompozycji.
// Usuń tę klasę i jej ustawienie w MauiProgram – ViewModel prosi o trasę, a zależności dostaje w konstruktorze.
public static class ServiceLocator
{
    public static IServiceProvider? Provider { get; set; }

    public static T Get<T>() where T : notnull => Provider!.GetRequiredService<T>();
}
