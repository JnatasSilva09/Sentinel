using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sentinel.Data;

public static class SentinelDbOptions
{
    public static DbContextOptions<SentinelDbContext> Criar(string connectionString)
        => new DbContextOptionsBuilder<SentinelDbContext>()
            .UseSqlite(connectionString)
            .Options;
}

/// <summary>Usada apenas pelas ferramentas do EF (dotnet ef migrations ...).</summary>
public class SentinelDbContextFactory : IDesignTimeDbContextFactory<SentinelDbContext>
{
    public SentinelDbContext CreateDbContext(string[] args)
        => new(SentinelDbOptions.Criar("Data Source=sentinel-design.db"));
}
