using SpacecraftWeatherApi.Data.Entities;

namespace SpacecraftWeatherApi.Repositories;

public interface IWeatherRepository
{
    Task<WeatherRecord> SaveAsync(string rawJson, CancellationToken cancellationToken = default);
    Task<WeatherRecord?> GetLatestAsync(CancellationToken cancellationToken = default);
}
