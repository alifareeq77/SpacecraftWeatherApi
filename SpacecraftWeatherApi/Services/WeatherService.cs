using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using SpacecraftWeatherApi.Configuration;
using SpacecraftWeatherApi.Repositories;

namespace SpacecraftWeatherApi.Services;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly IWeatherRepository _repository;
    private readonly WeatherServiceOptions _options;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(
        HttpClient httpClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        IWeatherRepository repository,
        IOptions<WeatherServiceOptions> options,
        ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _pipelineProvider = pipelineProvider;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetWeatherDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pipeline = _pipelineProvider.GetPipeline<string>("weather");

            var rawJson = await pipeline.ExecuteAsync(async ct =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _options.Url);

                if (!string.IsNullOrEmpty(_options.AuthScheme) && !string.IsNullOrEmpty(_options.AuthToken))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(_options.AuthScheme, _options.AuthToken);

                if (!string.IsNullOrEmpty(_options.ApiKey))
                    request.Headers.Add("X-Api-Key", _options.ApiKey);

                var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(ct);

                // Validate that the response is valid JSON
                using var doc = JsonDocument.Parse(content);

                return content;
            }, cancellationToken);

            await _repository.SaveAsync(rawJson, cancellationToken);
            _logger.LogInformation("Weather data fetched and persisted successfully");

            return rawJson;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch weather data from external service, falling back to cache");

            var cached = await _repository.GetLatestAsync(cancellationToken);
            if (cached is not null)
            {
                _logger.LogInformation("Returning cached weather data from {FetchedAt}", cached.FetchedAt);
                return cached.RawJson;
            }

            _logger.LogWarning("No cached weather data available");
            return null;
        }
    }
}
