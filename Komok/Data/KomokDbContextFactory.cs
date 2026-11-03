using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Komok.Data;

public sealed class KomokDbContextFactory : IDesignTimeDbContextFactory<KomokDbContext>
{
    public KomokDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var projectPath = Path.Combine(basePath, "Komok");
        if (!File.Exists(Path.Combine(basePath, "Komok.csproj"))
            && File.Exists(Path.Combine(projectPath, "Komok.csproj")))
        {
            basePath = projectPath;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("KomokDatabase")
            ?? "Host=localhost;Database=KomokDesign;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<KomokDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new KomokDbContext(options);
    }
}
