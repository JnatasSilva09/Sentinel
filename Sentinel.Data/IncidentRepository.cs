using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Services;

namespace Sentinel.Data;

public class IncidentRepository : IIncidentRepository
{
    private readonly DbContextOptions<SentinelDbContext> _opcoes;

    public IncidentRepository(DbContextOptions<SentinelDbContext> opcoes) => _opcoes = opcoes;

    private SentinelDbContext CriarContexto() => new(_opcoes);

    public int Adicionar(Incident incidente, IReadOnlyList<int> eventoIds)
    {
        using var db = CriarContexto();

        foreach (var eventoId in eventoIds.Distinct())
            incidente.Eventos.Add(new IncidentEvent { SecurityEventId = eventoId });

        db.Incidents.Add(incidente);
        try
        {
            db.SaveChanges(); // incidente + notas + vínculos em uma transação
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            // Índice único do alerta de origem: outra pessoa abriu o incidente no mesmo instante.
            throw new RegraDeNegocioException("Este alerta já virou um incidente.");
        }
        return incidente.Id;
    }

    public Incident? ObterCompleto(int id)
    {
        using var db = CriarContexto();
        return db.Incidents.AsNoTracking()
            .Include(i => i.Eventos).ThenInclude(ie => ie.SecurityEvent).ThenInclude(e => e.Device)
            .Include(i => i.Notas)
            .AsSplitQuery()
            .FirstOrDefault(i => i.Id == id);
    }

    public IReadOnlyList<Incident> Buscar(StatusIncidente? status, Gravidade? risco, string? termo)
    {
        using var db = CriarContexto();
        IQueryable<Incident> consulta = db.Incidents.AsNoTracking().Include(i => i.Eventos);

        if (status.HasValue)
        {
            var s = status.Value;
            consulta = consulta.Where(i => i.Status == s);
        }

        if (risco.HasValue)
        {
            var r = risco.Value;
            consulta = consulta.Where(i => i.Risco == r);
        }

        if (!string.IsNullOrWhiteSpace(termo))
        {
            var padrao = SqlLike.Contem(termo);
            consulta = consulta.Where(i => EF.Functions.Like(i.Titulo, padrao, SqlLike.Escape));
        }

        return consulta.OrderByDescending(i => i.DataAbertura).ThenByDescending(i => i.Id).ToList();
    }

    public Incident? PorAlerta(int alertaId)
    {
        using var db = CriarContexto();
        return db.Incidents.AsNoTracking().FirstOrDefault(i => i.AlertaOrigemId == alertaId);
    }

    public void Atualizar(Incident incidente, IncidentNote? nota)
    {
        using var db = CriarContexto();
        var existente = db.Incidents.Find(incidente.Id)
            ?? throw new InvalidOperationException($"Incidente {incidente.Id} não encontrado.");

        // Só os campos que mudam ao longo da vida do incidente; título e descrição ficam como foram abertos.
        existente.Status = incidente.Status;
        existente.Risco = incidente.Risco;
        existente.DataResolucao = incidente.DataResolucao;

        if (nota is not null)
        {
            nota.IncidentId = existente.Id;
            db.IncidentNotes.Add(nota);
        }

        db.SaveChanges();
    }
}
