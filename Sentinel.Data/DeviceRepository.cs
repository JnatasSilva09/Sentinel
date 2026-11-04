using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Services;

namespace Sentinel.Data;

/// <summary>
/// Cada operação usa um DbContext novo e descartável, o que evita problemas
/// de entidades rastreadas em uma aplicação WinForms de longa duração.
/// </summary>
public class DeviceRepository : IDeviceRepository
{
    private readonly DbContextOptions<SentinelDbContext> _opcoes;

    public DeviceRepository(DbContextOptions<SentinelDbContext> opcoes) => _opcoes = opcoes;

    private SentinelDbContext CriarContexto() => new(_opcoes);

    public IReadOnlyList<Device> Buscar(string? termo, SistemaOperacional? sistemaOperacional)
    {
        using var db = CriarContexto();
        IQueryable<Device> consulta = db.Devices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termo))
        {
            // O texto digitado é dado do usuário: % e _ viram caracteres literais, não curingas.
            var padrao = SqlLike.Contem(termo);
            consulta = consulta.Where(d =>
                EF.Functions.Like(d.Nome, padrao, SqlLike.Escape) ||
                EF.Functions.Like(d.EnderecoIp, padrao, SqlLike.Escape));
        }

        if (sistemaOperacional.HasValue)
        {
            var so = sistemaOperacional.Value;
            consulta = consulta.Where(d => d.SistemaOperacional == so);
        }

        return consulta.OrderBy(d => d.Nome).ToList();
    }

    public Device? ObterPorId(int id)
    {
        using var db = CriarContexto();
        return db.Devices.AsNoTracking().FirstOrDefault(d => d.Id == id);
    }

    public bool IpExiste(string enderecoIp, int? ignorarId)
    {
        using var db = CriarContexto();
        var idIgnorado = ignorarId ?? 0;
        return db.Devices.Any(d => d.EnderecoIp == enderecoIp && d.Id != idIgnorado);
    }

    public void Adicionar(Device device)
    {
        using var db = CriarContexto();
        db.Devices.Add(device);
        Salvar(db);
    }

    public void Atualizar(Device device)
    {
        using var db = CriarContexto();
        var existente = db.Devices.Find(device.Id)
            ?? throw new InvalidOperationException($"Dispositivo {device.Id} não encontrado.");

        // Copia só os campos editáveis; DataCadastro é preservada.
        existente.Nome = device.Nome;
        existente.EnderecoIp = device.EnderecoIp;
        existente.SistemaOperacional = device.SistemaOperacional;
        existente.Tipo = device.Tipo;
        Salvar(db);
    }

    public bool Remover(int id)
    {
        using var db = CriarContexto();
        var existente = db.Devices.Find(id);
        if (existente is null) return false;

        db.Devices.Remove(existente);
        db.SaveChanges();
        return true;
    }

    /// <summary>
    /// A checagem de IP duplicado no serviço pode perder uma corrida (dois acessos ao mesmo tempo).
    /// O índice único do banco é a garantia final; aqui ele vira uma mensagem amigável.
    /// </summary>
    private static void Salvar(SentinelDbContext db)
    {
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            throw new RegraDeNegocioException("Esse endereço IP já está em uso por outro dispositivo.");
        }
    }
}
