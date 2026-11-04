using Sentinel.Core.Entities;

namespace Sentinel.Core.Deteccao;

public interface IRegraDeteccao
{
    string Nome { get; }

    /// <summary>Período olhado para trás a partir do evento novo. Também é o tempo de "silêncio" entre alertas iguais.</summary>
    TimeSpan Janela { get; }

    /// <summary>
    /// Avalia um evento recém-chegado. <paramref name="anteriores"/> são os eventos já registrados do mesmo
    /// dispositivo (sem o novo). Retorna um alerta, ou null se o padrão não foi atingido.
    /// </summary>
    Alerta? Avaliar(SecurityEvent novo, IReadOnlyList<SecurityEvent> anteriores);
}
