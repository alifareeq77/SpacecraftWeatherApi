using Microsoft.EntityFrameworkCore;
using SpacecraftWeatherApi.Data;
using SpacecraftWeatherApi.Data.Entities;

namespace SpacecraftWeatherApi.Repositories;

public class WeatherRepository : IWeatherRepository
{
    private readonly WeatherDbContext _dbContext;

    public WeatherRepository(WeatherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WeatherRecord> SaveAsync(string rawJson, CancellationToken cancellationToken = default)
    {
        var record = new WeatherRecord
        {
            RawJson = rawJson,
            FetchedAt = DateTime.UtcNow
        };

        _dbContext.WeatherRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return record;
    }

    public async Task<WeatherRecord?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.WeatherRecords
            .OrderByDescending(r => r.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
