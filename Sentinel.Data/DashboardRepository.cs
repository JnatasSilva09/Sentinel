using Microsoft.EntityFrameworkCore;
using Sentinel.Core.Dashboard;
using Sentinel.Core.Enums;
using Sentinel.Core.Interfaces;

namespace Sentinel.Data;

/// <summary>Consultas de agregação feitas no banco (GROUP BY), sem trazer todos os registros para a memória.</summary>
public class DashboardRepository : IDashboardRepository
{
    private readonly DbContextOptions<SentinelDbContext> _opcoes;

    public DashboardRepository(DbContextOptions<SentinelDbContext> opcoes) => _opcoes = opcoes;

    private SentinelDbContext CriarContexto() => new(_opcoes);

    public int ContarDispositivos()
    {
        using var db = CriarContexto();
        return db.Devices.Count();
    }

    public IReadOnlyDictionary<Gravidade, int> EventosPorGravidade(DateTime desde)
    {
        using var db = CriarContexto();
        return db.Events.AsNoTracking()
            .Where(e => e.DataHora >= desde)
            .GroupBy(e => e.Gravidade)
            .Select(g => new { Chave = g.Key, Total = g.Count() })
            .ToList()
            .ToDictionary(x => x.Chave, x => x.Total);
    }

    public IReadOnlyDictionary<TipoEvento, int> EventosPorTipo(DateTime desde)
    {
        using var db = CriarContexto();
        return db.Events.AsNoTracking()
            .Where(e => e.DataHora >= desde)
            .GroupBy(e => e.Tipo)
            .Select(g => new { Chave = g.Key, Total = g.Count() })
            .ToList()
            .ToDictionary(x => x.Chave, x => x.Total);
    }

    public IReadOnlyDictionary<Gravidade, int> AlertasPorGravidade(DateTime desde)
    {
        using var db = CriarContexto();
        return db.Alerts.AsNoTracking()
            .Where(a => a.DataHora >= desde)
            .GroupBy(a => a.Gravidade)
            .Select(g => new { Chave = g.Key, Total = g.Count() })
            .ToList()
            .ToDictionary(x => x.Chave, x => x.Total);
    }

    public IReadOnlyDictionary<StatusIncidente, int> IncidentesPorStatus()
    {
        using var db = CriarContexto();
        return db.Incidents.AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { Chave = g.Key, Total = g.Count() })
            .ToList()
            .ToDictionary(x => x.Chave, x => x.Total);
    }

    public IReadOnlyList<ItemRanking> DispositivosMaisAtivos(DateTime desde, int quantidade)
    {
        using var db = CriarContexto();

        var topo = db.Events.AsNoTracking()
            .Where(e => e.DataHora >= desde)
            .GroupBy(e => e.DeviceId)
            .Select(g => new { DeviceId = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total).ThenBy(x => x.DeviceId)
            .Take(quantidade)
            .ToList();

        var ids = topo.Select(x => x.DeviceId).ToList();
        var nomes = db.Devices.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .ToDictionary(d => d.Id, d => d.Nome);

        return topo.Select(x => new ItemRanking(nomes.GetValueOrDefault(x.DeviceId, "(removido)"), x.Total)).ToList();
    }

    public IReadOnlyList<DateTime> DatasDosEventos(DateTime desde)
    {
        using var db = CriarContexto();
        return db.Events.AsNoTracking()
            .Where(e => e.DataHora >= desde)
            .Select(e => e.DataHora)
            .ToList();
    }
}
