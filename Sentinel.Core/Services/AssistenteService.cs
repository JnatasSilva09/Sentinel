using Sentinel.Core.Assistente;
using Sentinel.Core.Entities;
using Sentinel.Core.Interfaces;

namespace Sentinel.Core.Services;

/// <summary>
/// Monta o contexto (curto e sem dados desnecessários) a partir do banco e chama o assistente.
/// Nunca altera dados sozinho: o assistente só sugere.
/// </summary>
public class AssistenteService
{
    private const int MaximoDeEventos = 10;
    private const int MaximoDeNotas = 10;
    private const int MaximoDeCaracteres = 300;
    private const int JanelaDoAlertaEmMinutos = 60;
    public const int JanelaDeRepeticaoEmMinutos = 10;

    private readonly IAssistenteIA _assistente;
    private readonly IMonitoramentoRepository _monitoramento;
    private readonly IncidentService _incidentes;

    public AssistenteService(IAssistenteIA assistente, IMonitoramentoRepository monitoramento, IncidentService incidentes)
    {
        _assistente = assistente;
        _monitoramento = monitoramento;
        _incidentes = incidentes;
    }

    public string NomeDoAssistente => _assistente.Nome;

    public async Task<AnaliseAlerta> ExplicarAlertaAsync(int alertaId, CancellationToken ct = default)
    {
        var alerta = _monitoramento.ObterAlerta(alertaId)
            ?? throw new RegraDeNegocioException("Alerta não encontrado. Ele pode ter sido apagado.");

        var eventos = _monitoramento
            .EventosDoDispositivo(alerta.DeviceId, alerta.DataHora.AddMinutes(-JanelaDoAlertaEmMinutos), alerta.DataHora)
            .TakeLast(MaximoDeEventos)
            .Select(ParaContexto)
            .ToList();

        var contexto = new ContextoAlerta(
            alerta.Id, alerta.Regra, alerta.Gravidade, Cortar(alerta.Descricao), alerta.DataHora,
            ParaContexto(alerta.Device), eventos);

        return await _assistente.ExplicarAlertaAsync(contexto, ct);
    }

    public async Task<ResumoIncidente> ResumirIncidenteAsync(int incidenteId, CancellationToken ct = default)
    {
        var incidente = _incidentes.Obter(incidenteId);

        var todosOsEventos = incidente.Eventos.Select(v => v.SecurityEvent).OrderBy(e => e.DataHora).ToList();
        var contexto = new ContextoIncidente(
            incidente.Id,
            Cortar(incidente.Titulo),
            Cortar(incidente.Descricao),
            incidente.Risco,
            incidente.Status,
            incidente.DataAbertura,
            incidente.DataResolucao,
            todosOsEventos.Count,
            todosOsEventos.TakeLast(MaximoDeEventos).Select(ParaContexto).ToList(),
            todosOsEventos.Select(e => e.Device.Nome).Distinct().ToList(),
            incidente.Notas.Count,
            incidente.Notas.TakeLast(MaximoDeNotas).Select(n => new ContextoNota(n.DataHora, Cortar(n.Texto))).ToList());

        return await _assistente.ResumirIncidenteAsync(contexto, ct);
    }

    public async Task<ClassificacaoSugerida> SugerirGravidadeAsync(int eventoId, CancellationToken ct = default)
    {
        var evento = _monitoramento.ObterEvento(eventoId)
            ?? throw new RegraDeNegocioException("Evento não encontrado. Ele pode ter sido apagado.");

        var repeticoes = _monitoramento
            .EventosDoDispositivo(evento.DeviceId, evento.DataHora.AddMinutes(-JanelaDeRepeticaoEmMinutos), evento.DataHora)
            .Count(e => e.Id != evento.Id && e.Tipo == evento.Tipo);

        var contexto = new ContextoClassificacao(
            ParaContexto(evento), ParaContexto(evento.Device), repeticoes, JanelaDeRepeticaoEmMinutos);

        return await _assistente.SugerirGravidadeAsync(contexto, ct);
    }

    private static ContextoDispositivo ParaContexto(Device d)
        => new(Cortar(d.Nome), d.SistemaOperacional, d.Tipo);

    private static ContextoEvento ParaContexto(SecurityEvent e)
        => new(e.DataHora, e.Tipo, e.Gravidade, Cortar(e.Descricao));

    private static string Cortar(string texto)
        => texto.Length <= MaximoDeCaracteres ? texto : texto[..(MaximoDeCaracteres - 1)] + "…";
}
