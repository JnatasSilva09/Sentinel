using Sentinel.Core.Dashboard;
using Sentinel.Core.Enums;

namespace Sentinel.Core.Interfaces;

public interface IDashboardRepository
{
    int ContarDispositivos();
    IReadOnlyDictionary<Gravidade, int> EventosPorGravidade(DateTime desde);
    IReadOnlyDictionary<TipoEvento, int> EventosPorTipo(DateTime desde);
    IReadOnlyDictionary<Gravidade, int> AlertasPorGravidade(DateTime desde);
    IReadOnlyDictionary<StatusIncidente, int> IncidentesPorStatus();
    IReadOnlyList<ItemRanking> DispositivosMaisAtivos(DateTime desde, int quantidade);
    IReadOnlyList<DateTime> DatasDosEventos(DateTime desde);
}
