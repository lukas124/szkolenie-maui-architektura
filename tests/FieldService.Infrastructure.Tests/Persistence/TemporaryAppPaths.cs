using FieldService.Application.Abstractions;

namespace FieldService.Infrastructure.Tests.Persistence;

// Katalog danych (i cache w podkatalogu) w unikalnym folderze tymczasowym; Dispose sprząta po teście.
public sealed class TemporaryAppPaths : IAppPaths, IDisposable
{
    public TemporaryAppPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "fieldservice-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDirectory);
        CacheDirectory = Path.Combine(DataDirectory, "cache");
    }

    public string DataDirectory { get; }

    public string CacheDirectory { get; }

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
