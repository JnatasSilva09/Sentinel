using Sentinel.Core.Dashboard;
using Sentinel.Core.Enums;
using Sentinel.Core.Interfaces;

namespace Sentinel.Core.Services;

public class DashboardService
{
    public const int QuantidadeNoRanking = 5;

    private readonly IDashboardRepository _repositorio;
    private readonly Func<DateTime> _agora;

    public DashboardService(IDashboardRepository repositorio, Func<DateTime>? agora = null)
    {
        _repositorio = repositorio;
        _agora = agora ?? (() => DateTime.Now);
    }

    public ResumoDashboard Obter(PeriodoDashboard periodo)
    {
        if (!Enum.IsDefined(periodo)) throw new RegraDeNegocioException("Período inválido.");

        var agora = _agora();
        var intervalo = periodo.Intervalo();
        var duracao = periodo.Duracao();

        // A janela termina no fim da coluna atual (ex.: 11:00 se agora são 10:20 e as colunas são de 1 h),
        // assim as colunas ficam alinhadas ao relógio e a última sempre contém o momento presente.
        var fim = new DateTime((agora.Ticks / intervalo.Ticks + 1) * intervalo.Ticks, agora.Kind);
        var desde = fim - duracao;

        var porGravidade = Completar(_repositorio.EventosPorGravidade(desde));
        var porTipo = Completar(_repositorio.EventosPorTipo(desde));
        var alertas = Completar(_repositorio.AlertasPorGravidade(desde));
        var incidentes = Completar(_repositorio.IncidentesPorStatus());

        return new ResumoDashboard(
            periodo, desde, fim,
            TotalDispositivos: _repositorio.ContarDispositivos(),
            TotalEventos: porGravidade.Values.Sum(),
            TotalAlertas: alertas.Values.Sum(),
            IncidentesEmAndamento: incidentes[StatusIncidente.Aberto] + incidentes[StatusIncidente.EmAnalise],
            EventosPorGravidade: porGravidade,
            EventosPorTipo: porTipo,
            AlertasPorGravidade: alertas,
            IncidentesPorStatus: incidentes,
            DispositivosMaisAtivos: _repositorio.DispositivosMaisAtivos(desde, QuantidadeNoRanking),
            EventosAoLongoDoTempo: SerieTemporal.Agrupar(_repositorio.DatasDosEventos(desde), fim, duracao, intervalo));
    }

    /// <summary>Garante uma entrada para cada valor do enum (zero quando não houve ocorrência).</summary>
    private static IReadOnlyDictionary<T, int> Completar<T>(IReadOnlyDictionary<T, int> origem) where T : struct, Enum
        => Enum.GetValues<T>().ToDictionary(v => v, v => origem.TryGetValue(v, out var n) ? n : 0);
}
