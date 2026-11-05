using Microsoft.EntityFrameworkCore;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Interfaces;

namespace Sentinel.Data;

public class MonitoramentoRepository : IMonitoramentoRepository
{
    private readonly DbContextOptions<SentinelDbContext> _opcoes;

    public MonitoramentoRepository(DbContextOptions<SentinelDbContext> opcoes) => _opcoes = opcoes;

    private SentinelDbContext CriarContexto() => new(_opcoes);

    public IReadOnlyList<SecurityEvent> EventosDoDispositivo(int deviceId, DateTime desde, DateTime ate)
    {
        using var db = CriarContexto();
        return db.Events.AsNoTracking()
            .Where(e => e.DeviceId == deviceId && e.DataHora >= desde && e.DataHora <= ate)
            .OrderBy(e => e.DataHora)
            .ToList();
    }

    public IReadOnlyList<Alerta> AlertasDoDispositivo(int deviceId, DateTime desde)
    {
        using var db = CriarContexto();
        return db.Alerts.AsNoTracking()
            .Where(a => a.DeviceId == deviceId && a.DataHora >= desde)
            .OrderBy(a => a.DataHora)
            .ToList();
    }

    public SecurityEvent? ObterEvento(int id)
    {
        using var db = CriarContexto();
        return db.Events.AsNoTracking().Include(e => e.Device).FirstOrDefault(e => e.Id == id);
    }

    public void AtualizarGravidadeDoEvento(int id, Gravidade gravidade)
    {
        using var db = CriarContexto();
        var existente = db.Events.Find(id)
            ?? throw new InvalidOperationException($"Evento {id} não encontrado.");
        existente.Gravidade = gravidade;
        db.SaveChanges();
    }

    public Alerta? ObterAlerta(int id)
    {
        using var db = CriarContexto();
        return db.Alerts.AsNoTracking().Include(a => a.Device).FirstOrDefault(a => a.Id == id);
    }

    public void Registrar(SecurityEvent evento, IReadOnlyList<Alerta> alertas)
    {
        using var db = CriarContexto();
        db.Events.Add(evento);
        db.Alerts.AddRange(alertas);
        db.SaveChanges(); // uma única chamada = uma única transação
    }

    public IReadOnlyList<SecurityEvent> UltimosEventos(int quantidade)
    {
        using var db = CriarContexto();
        return db.Events.AsNoTracking()
            .Include(e => e.Device)
            .OrderByDescending(e => e.DataHora).ThenByDescending(e => e.Id)
            .Take(quantidade)
            .ToList();
    }

    public IReadOnlyList<Alerta> UltimosAlertas(int quantidade)
    {
        using var db = CriarContexto();
        return db.Alerts.AsNoTracking()
            .Include(a => a.Device)
            .OrderByDescending(a => a.DataHora).ThenByDescending(a => a.Id)
            .Take(quantidade)
            .ToList();
    }

    public void LimparEventosEAlertas()
    {
        using var db = CriarContexto();
        using var transacao = db.Database.BeginTransaction();
        db.Alerts.ExecuteDelete();
        db.Events.ExecuteDelete();
        transacao.Commit();
    }
}
