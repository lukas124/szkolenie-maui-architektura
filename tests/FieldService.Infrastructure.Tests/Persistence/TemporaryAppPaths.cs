using FieldService.Application.Abstractions;

namespace FieldService.Infrastructure.Tests.Persistence;

// Katalog danych w unikalnym folderze tymczasowym; Dispose sprząta po teście.
public sealed class TemporaryAppPaths : IAppPaths, IDisposable
{
    public TemporaryAppPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "fieldservice-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Plik bazy bywa jeszcze otwarty przez pulę połączeń – katalog tymczasowy posprząta system.
        }
        catch (UnauthorizedAccessException)
        {
            // jw.
        }
    }
}
