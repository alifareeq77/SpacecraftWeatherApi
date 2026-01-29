using Microsoft.EntityFrameworkCore;
using SpacecraftWeatherApi.Data.Entities;

namespace SpacecraftWeatherApi.Data;

public class WeatherDbContext : DbContext
{
    public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options)
    {
    }

    public DbSet<WeatherRecord> WeatherRecords => Set<WeatherRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeatherRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FetchedAt);
        });
    }
}
