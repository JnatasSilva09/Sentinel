using Microsoft.EntityFrameworkCore;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;

namespace Sentinel.Data;

public static class DatabaseInitializer
{
    /// <summary>Aplica as migrations e, se o banco acabou de ser criado, insere dados de exemplo.</summary>
    public static void Inicializar(DbContextOptions<SentinelDbContext> opcoes)
    {
        using var db = new SentinelDbContext(opcoes);

        if (!db.Database.GetMigrations().Any())
        {
            throw new InvalidOperationException(
                "Nenhuma migration encontrada. Rode na pasta da solução:" + Environment.NewLine +
                "dotnet ef migrations add Inicial --project Sentinel.Data");
        }

        var bancoNovo = !db.Database.GetAppliedMigrations().Any();
        db.Database.Migrate();

        if (bancoNovo)
            SeedData.Popular(db);
    }
}

public static class SeedData
{
    /// <summary>Dispositivos fictícios para demonstração. Nenhum corresponde a uma rede real.</summary>
    public static void Popular(SentinelDbContext db)
    {
        if (db.Devices.Any()) return;

        var agora = DateTime.Now;
        db.Devices.AddRange(
            Novo("PC-Financeiro-01", "192.168.1.10", SistemaOperacional.Windows, TipoDispositivo.Desktop, agora),
            Novo("PC-RH-02", "192.168.1.11", SistemaOperacional.Windows, TipoDispositivo.Desktop, agora),
            Novo("NB-Diretoria", "192.168.1.20", SistemaOperacional.MacOS, TipoDispositivo.Notebook, agora),
            Novo("SRV-Arquivos", "192.168.1.100", SistemaOperacional.Linux, TipoDispositivo.Servidor, agora),
            Novo("SRV-Web", "192.168.1.101", SistemaOperacional.Linux, TipoDispositivo.Servidor, agora),
            Novo("RT-Principal", "192.168.1.1", SistemaOperacional.Linux, TipoDispositivo.Roteador, agora),
            Novo("Celular-Vendas", "192.168.1.50", SistemaOperacional.Android, TipoDispositivo.Smartphone, agora));

        db.SaveChanges();
    }

    private static Device Novo(string nome, string ip, SistemaOperacional so, TipoDispositivo tipo, DateTime data)
        => new()
        {
            Nome = nome,
            EnderecoIp = ip,
            SistemaOperacional = so,
            Tipo = tipo,
            DataCadastro = data
        };
}
