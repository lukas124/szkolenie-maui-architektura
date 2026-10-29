namespace FieldService.UI.Extensions;

public static class TaskExtensions
{
    // Świadomy fire-and-forget: jedyne dozwolone „async void” – wyjątek trafia do logu, nie w próżnię
    // (od checkpointu 15-monitoring także do raportu awarii)
    public static async void FireAndForget(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FieldService] Nieobsłużony wyjątek w zadaniu w tle: {ex}");
        }
    }
}
