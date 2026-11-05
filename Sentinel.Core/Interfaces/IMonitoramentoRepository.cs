using Sentinel.Core.Entities;
using Sentinel.Core.Enums;

namespace Sentinel.Core.Interfaces;

public interface IMonitoramentoRepository
{
    /// <summary>Eventos de um dispositivo no intervalo [desde, ate], em ordem cronológica.</summary>
    IReadOnlyList<SecurityEvent> EventosDoDispositivo(int deviceId, DateTime desde, DateTime ate);

    IReadOnlyList<Alerta> AlertasDoDispositivo(int deviceId, DateTime desde);

    /// <summary>Evento com o dispositivo carregado, ou null se não existir.</summary>
    SecurityEvent? ObterEvento(int id);

    void AtualizarGravidadeDoEvento(int id, Gravidade gravidade);

    /// <summary>Alerta com o dispositivo carregado, ou null se não existir.</summary>
    Alerta? ObterAlerta(int id);

    /// <summary>Grava o evento e seus alertas na mesma transação: ou salva tudo, ou nada.</summary>
    void Registrar(SecurityEvent evento, IReadOnlyList<Alerta> alertas);

    /// <summary>Mais recentes primeiro, já com o dispositivo carregado.</summary>
    IReadOnlyList<SecurityEvent> UltimosEventos(int quantidade);

    IReadOnlyList<Alerta> UltimosAlertas(int quantidade);

    void LimparEventosEAlertas();
}
