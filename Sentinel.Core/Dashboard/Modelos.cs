using Sentinel.Core.Enums;

namespace Sentinel.Core.Dashboard;

public enum PeriodoDashboard
{
    UltimaHora,
    Ultimas24Horas,
    Ultimos7Dias
}

public static class PeriodoDashboardExtensions
{
    public static TimeSpan Duracao(this PeriodoDashboard p) => p switch
    {
        PeriodoDashboard.UltimaHora => TimeSpan.FromHours(1),
        PeriodoDashboard.Ultimas24Horas => TimeSpan.FromHours(24),
        _ => TimeSpan.FromDays(7)
    };

    /// <summary>Tamanho de cada coluna do gráfico de linha do tempo.</summary>
    public static TimeSpan Intervalo(this PeriodoDashboard p) => p switch
    {
        PeriodoDashboard.UltimaHora => TimeSpan.FromMinutes(5),
        PeriodoDashboard.Ultimas24Horas => TimeSpan.FromHours(1),
        _ => TimeSpan.FromDays(1)
    };

    public static string Rotulo(this PeriodoDashboard p) => p switch
    {
        PeriodoDashboard.UltimaHora => "Última hora",
        PeriodoDashboard.Ultimas24Horas => "Últimas 24 horas",
        _ => "Últimos 7 dias"
    };

    public static string FormatoDoRotulo(this PeriodoDashboard p) => p switch
    {
        PeriodoDashboard.UltimaHora => "HH:mm",
        PeriodoDashboard.Ultimas24Horas => "HH'h'",
        _ => "dd/MM"
    };
}

public sealed record ItemRanking(string Nome, int Total);

public sealed record PontoSerie(DateTime Inicio, int Total);

public sealed record ResumoDashboard(
    PeriodoDashboard Periodo,
    DateTime Desde,
    DateTime Ate,
    int TotalDispositivos,
    int TotalEventos,
    int TotalAlertas,
    int IncidentesEmAndamento,
    IReadOnlyDictionary<Gravidade, int> EventosPorGravidade,
    IReadOnlyDictionary<TipoEvento, int> EventosPorTipo,
    IReadOnlyDictionary<Gravidade, int> AlertasPorGravidade,
    IReadOnlyDictionary<StatusIncidente, int> IncidentesPorStatus,
    IReadOnlyList<ItemRanking> DispositivosMaisAtivos,
    IReadOnlyList<PontoSerie> EventosAoLongoDoTempo);
