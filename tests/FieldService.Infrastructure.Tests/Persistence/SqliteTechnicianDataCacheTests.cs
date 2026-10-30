using FieldService.Application.Abstractions;
using FieldService.Infrastructure.Persistence;
using FieldService.Infrastructure.Tests.Caching;
using Moq;
using NUnit.Framework;

namespace FieldService.Infrastructure.Tests.Persistence;

[TestFixture]
public sealed class SqliteTechnicianDataCacheTests
{
    private TemporaryAppPaths _paths = null!;
    private SqliteDb _db = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new TemporaryAppPaths();
        _db = new SqliteDb(_paths);
    }

    [TearDown]
    public async Task TearDown()
    {
        await CacheTestHelpers.DisposeDbAsync(_db);
        _paths.Dispose();
    }

    // Zmiana technika (ClearAsync danych lokalnych) czyści też lokalne cache
    [Test]
    public async Task ClearAsync_TechnicianChanged_ClearsLocalCaches()
    {
        var caches = new Mock<ILocalCaches>();
        var data = new SqliteTechnicianData(_db, caches.Object);

        // ZAŁOŻENIE: ILocalTechnicianData.ClearAsync(CancellationToken)
        await data.ClearAsync(CancellationToken.None);

        caches.Verify(c => c.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
