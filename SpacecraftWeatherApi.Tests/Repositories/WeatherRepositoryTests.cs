using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpacecraftWeatherApi.Data;
using SpacecraftWeatherApi.Repositories;
using Xunit;

namespace SpacecraftWeatherApi.Tests.Repositories;

public class WeatherRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WeatherDbContext _dbContext;
    private readonly WeatherRepository _repository;

    public WeatherRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<WeatherDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new WeatherDbContext(options);
        _dbContext.Database.EnsureCreated();

        _repository = new WeatherRepository(_dbContext);
    }

    [Fact]
    public async Task SaveAsync_PersistsRecord()
    {
        var json = """{"temperature":20}""";

        var record = await _repository.SaveAsync(json);

        var saved = await _dbContext.WeatherRecords.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(json, saved.RawJson);
    }

    [Fact]
    public async Task SaveAsync_SetsFetchedAtToUtcNow()
    {
        var before = DateTime.UtcNow;

        var record = await _repository.SaveAsync("""{"temperature":20}""");

        var after = DateTime.UtcNow;
        Assert.InRange(record.FetchedAt, before, after);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsLatestByFetchedAt()
    {
        var older = new Data.Entities.WeatherRecord
        {
            RawJson = """{"old":true}""",
            FetchedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var newer = new Data.Entities.WeatherRecord
        {
            RawJson = """{"new":true}""",
            FetchedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        _dbContext.WeatherRecords.AddRange(older, newer);
        await _dbContext.SaveChangesAsync();

        var latest = await _repository.GetLatestAsync();

        Assert.NotNull(latest);
        Assert.Equal("""{"new":true}""", latest.RawJson);
    }

    [Fact]
    public async Task GetLatestAsync_EmptyDb_ReturnsNull()
    {
        var result = await _repository.GetLatestAsync();

        Assert.Null(result);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
