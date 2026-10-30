using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FieldService.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace FieldService.Infrastructure.Caching;

// Miniatury: pamięć (z limitem rozmiaru) → plik w katalogu cache → sieć. Bez limitu cache to wyciek z ładną nazwą –
// każda przewinięta miniatura zostaje w pamięci na zawsze. Katalog plików daje platforma (IAppPaths), reszta to czysty .NET.
public sealed class ThumbnailCache : IThumbnailCache
{
    private readonly Func<HttpClient> _http;
    private readonly string _directory;
    private readonly MemoryCache _memory;
    // miniatury, które są w pamięci (MemoryCache liczy rozmiar tylko z limitem – pomiar „bez limitu” też go potrzebuje)
    private readonly ConcurrentDictionary<string, byte[]> _inMemory = new();
    private readonly Lock _rememberLock = new();

    // http – fabryka klienta wołana przy każdym pobraniu (singleton nie trzyma HttpClient)
    public ThumbnailCache(Func<HttpClient> http, IAppPaths paths, CachingOptions options)
    {
        _http = http;
        _directory = Path.Combine(paths.CacheDirectory, "thumbnails");
        // SizeLimit liczony w bajtach: każdy wpis podaje swój rozmiar (Size), null = bez limitu
        _memory = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = options.ThumbnailMemoryLimitBytes > 0 ? options.ThumbnailMemoryLimitBytes : null
        });
    }

    // liczba miniatur na dysku (Diagnostyka)
    public int Count => Directory.Exists(_directory) ? Directory.GetFiles(_directory).Length : 0;

    // suma rozmiarów miniatur w pamięci (Diagnostyka, pomiar)
    public long MemoryBytes => _inMemory.Values.Sum(bytes => bytes.LongLength);

    public async Task<Stream> OpenAsync(string url, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); // wiersz zniknął z ekranu – nie pobieramy
        if (_memory.TryGetValue(url, out byte[]? bytes) && bytes is not null) return new MemoryStream(bytes, writable: false);

        var file = FileFor(url);
        if (File.Exists(file))
        {
            bytes = await File.ReadAllBytesAsync(file, ct);
            Remember(url, bytes);
            return new MemoryStream(bytes, writable: false);
        }

        bytes = await DownloadAsync(url, ct);
        if (bytes is null) return Stream.Null;

        Save(file, bytes);
        Remember(url, bytes);
        return new MemoryStream(bytes, writable: false);
    }

    // Plik zablokowany albo w trakcie zapisu – wylogowanie nie może się przez to wywrócić (zostanie do następnego czyszczenia)
    public void Clear()
    {
        _memory.Clear();
        // MemoryCache zgłasza usunięte wpisy później, na innym wątku – słownik czyścimy od razu
        _inMemory.Clear();
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Zapis atomowy: najpierw plik tymczasowy, potem zmiana nazwy – drugi wiersz z tą samą miniaturą nigdy nie przeczyta
    // połowy pliku. Błąd zapisu nie psuje miniatury (zostaje w pamięci).
    private void Save(string file, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var temporary = $"{file}.{Guid.NewGuid():N}.tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // brak sieci albo błąd serwera → null (pusta miniatura), nie wyjątek. Anulowanie idzie dalej
    private async Task<byte[]?> DownloadAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return null; // brak adresu miniatury → pusta miniatura
        try
        {
            using var response = await _http().GetAsync(url, ct);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(ct) : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    // rozmiar wpisu = liczba bajtów. Ponad limit MemoryCache wypiera stare wpisy (albo nie przyjmuje nowego)
    private void Remember(string url, byte[] bytes)
    {
        var entry = new MemoryCacheEntryOptions { Size = bytes.LongLength };
        // MemoryCache zgłasza usunięcie później, na innym wątku: zdejmujemy tylko ten sam obiekt – spóźnione zgłoszenie
        // starego wpisu (podmiana tym samym adresem, Clear) nie skasuje nowego
        entry.RegisterPostEvictionCallback((key, value, _, _) =>
            _inMemory.TryRemove(KeyValuePair.Create((string)key, (byte[])value!)));
        // dwa pobrania tej samej miniatury naraz: słownik i pamięć zmieniamy razem, więc trzymają ten sam obiekt
        lock (_rememberLock)
        {
            _inMemory[url] = bytes;
            _memory.Set(url, bytes, entry);
        }
    }

    private string FileFor(string url) =>
        Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".png");
}
