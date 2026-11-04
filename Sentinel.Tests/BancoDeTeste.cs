using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sentinel.Data;

namespace Sentinel.Tests;

/// <summary>SQLite em memória com o esquema completo. Vive enquanto a conexão estiver aberta.</summary>
internal sealed class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");

    public DbContextOptions<SentinelDbContext> Opcoes { get; }

    public BancoDeTeste()
    {
        _conexao.Open();
        Opcoes = new DbContextOptionsBuilder<SentinelDbContext>().UseSqlite(_conexao).Options;
        using var db = new SentinelDbContext(Opcoes);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();
}
