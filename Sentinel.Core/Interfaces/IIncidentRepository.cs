using Sentinel.Core.Entities;
using Sentinel.Core.Enums;

namespace Sentinel.Core.Interfaces;

public interface IIncidentRepository
{
    /// <summary>Grava o incidente, suas notas e o vínculo com os eventos na mesma transação.</summary>
    int Adicionar(Incident incidente, IReadOnlyList<int> eventoIds);

    /// <summary>Incidente com eventos (e seus dispositivos) e notas carregados.</summary>
    Incident? ObterCompleto(int id);

    /// <summary>Mais recentes primeiro. Os vínculos com eventos vêm carregados (para contagem).</summary>
    IReadOnlyList<Incident> Buscar(StatusIncidente? status, Gravidade? risco, string? termo);

    Incident? PorAlerta(int alertaId);

    /// <summary>Atualiza status, risco e data de resolução e grava a nota de auditoria junto.</summary>
    void Atualizar(Incident incidente, IncidentNote? nota);
}
