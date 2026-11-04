using Sentinel.Core.Entities;
using Sentinel.Core.Enums;

namespace Sentinel.Core.Deteccao;

/// <summary>Aplica todas as regras a cada evento novo e evita alertas repetidos para o mesmo problema.</summary>
public sealed class MotorDeDeteccao
{
    private readonly IReadOnlyList<IRegraDeteccao> _regras;

    public MotorDeDeteccao(IEnumerable<IRegraDeteccao> regras) => _regras = regras.ToList();

    public IReadOnlyList<IRegraDeteccao> Regras => _regras;

    /// <summary>Quão longe no passado é preciso buscar eventos para que todas as regras funcionem.</summary>
    public TimeSpan JanelaMaxima => _regras.Count == 0 ? TimeSpan.Zero : _regras.Max(r => r.Janela);

    public static MotorDeDeteccao Padrao() => new(new IRegraDeteccao[]
    {
        new RegraPorContagem(
            "Força bruta de login", TipoEvento.LoginFalho, limite: 5, TimeSpan.FromMinutes(2), Gravidade.Alta,
            "{n} tentativas de login falhas em até {janela}. Possível ataque de força bruta: confira a origem e considere bloquear a conta ou o IP."),

        new RegraPorContagem(
            "Varredura de portas repetida", TipoEvento.VarreduraDePortas, limite: 3, TimeSpan.FromMinutes(10), Gravidade.Alta,
            "{n} varreduras de portas em até {janela}. Alguém pode estar mapeando os serviços expostos deste dispositivo."),

        new RegraPorContagem(
            "Acessos fora do horário", TipoEvento.AcessoForaDoHorario, limite: 2, TimeSpan.FromMinutes(60), Gravidade.Media,
            "{n} acessos fora do horário comercial em até {janela}. Confirme com o usuário se foram legítimos."),

        new RegraPorContagem(
            "Malware detectado", TipoEvento.MalwareDetectado, limite: 1, TimeSpan.FromMinutes(5), Gravidade.Critica,
            "Malware detectado neste dispositivo. Isole-o da rede e faça uma verificação completa antes de religar.")
    });

    /// <param name="novo">Evento que acabou de chegar.</param>
    /// <param name="anteriores">Eventos já registrados do mesmo dispositivo (sem o novo).</param>
    /// <param name="alertasRecentes">Alertas já emitidos para o mesmo dispositivo (para não repetir).</param>
    public IReadOnlyList<Alerta> Avaliar(
        SecurityEvent novo,
        IReadOnlyList<SecurityEvent> anteriores,
        IReadOnlyList<Alerta> alertasRecentes)
    {
        var novos = new List<Alerta>();

        foreach (var regra in _regras)
        {
            var alerta = regra.Avaliar(novo, anteriores);
            if (alerta is null) continue;

            // Já alertamos esta mesma regra neste dispositivo dentro da janela? Então é o mesmo incidente em andamento.
            var limite = novo.DataHora - regra.Janela;
            var jaAlertado = alertasRecentes.Any(a =>
                a.DeviceId == novo.DeviceId && a.Regra == regra.Nome && a.DataHora >= limite);

            if (!jaAlertado)
                novos.Add(alerta);
        }

        return novos;
    }
}
