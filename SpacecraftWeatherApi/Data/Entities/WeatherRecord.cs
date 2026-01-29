namespace SpacecraftWeatherApi.Data.Entities;

public class WeatherRecord
{
    public long Id { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
}
