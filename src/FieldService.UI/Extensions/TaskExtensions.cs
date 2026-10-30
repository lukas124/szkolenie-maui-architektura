namespace FieldService.UI.Extensions;

public static class TaskExtensions
{
    // Świadomy fire-and-forget: jedyne dozwolone „async void” – wyjątek trafia do logu i do raportu awarii,
    // nie w próżnię
    public static async void FireAndForget(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FieldService] Nieobsłużony wyjątek w zadaniu w tle: {ex}");
            Telemetry.CrashHandlers.ReportHandled(ex);
        }
    }
}
