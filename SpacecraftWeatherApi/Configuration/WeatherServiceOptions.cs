namespace SpacecraftWeatherApi.Configuration;

public class WeatherServiceOptions
{
    public const string SectionName = "WeatherService";

    public string Url { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 4000;
    public int RetryCount { get; set; } = 2;
    public int RetryBaseDelayMs { get; set; } = 200;

    /// <summary>
    /// Authorization scheme (e.g. "Bearer", "Basic"). Sent as the Authorization header when both this and AuthToken are set.
    /// </summary>
    public string AuthScheme { get; set; } = string.Empty;

    /// <summary>
    /// Authorization token/credential value. Sent as the Authorization header when both this and AuthScheme are set.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// API key sent via the X-Api-Key header when set.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
