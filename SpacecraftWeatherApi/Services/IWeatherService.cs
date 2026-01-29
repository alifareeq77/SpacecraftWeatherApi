namespace SpacecraftWeatherApi.Services;

public interface IWeatherService
{
    Task<string?> GetWeatherDataAsync(CancellationToken cancellationToken = default);
}
