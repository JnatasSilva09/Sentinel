using Sentinel.Core.Entities;
using Sentinel.Core.Enums;

namespace Sentinel.Core.Deteccao;

/// <summary>
/// Regra clássica de SOC: "N eventos de um tipo, no mesmo dispositivo, dentro de uma janela de tempo".
/// Exemplo: 5 logins falhos em 2 minutos (indício de força bruta).
/// </summary>
public sealed class RegraPorContagem : IRegraDeteccao
{
    private readonly string _mensagem;

    public string Nome { get; }
    public TipoEvento Tipo { get; }
    public int Limite { get; }
    public TimeSpan Janela { get; }
    public Gravidade Gravidade { get; }

    /// <param name="mensagem">Texto do alerta. Pode usar {n} (quantidade encontrada) e {janela} (ex.: "2 min").</param>
    public RegraPorContagem(string nome, TipoEvento tipo, int limite, TimeSpan janela, Gravidade gravidade, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("A regra precisa de um nome.", nameof(nome));
        if (limite < 1) throw new ArgumentOutOfRangeException(nameof(limite), "O limite deve ser pelo menos 1.");
        if (janela <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(janela), "A janela deve ser positiva.");

        Nome = nome;
        Tipo = tipo;
        Limite = limite;
        Janela = janela;
        Gravidade = gravidade;
        _mensagem = mensagem;
    }

    public Alerta? Avaliar(SecurityEvent novo, IReadOnlyList<SecurityEvent> anteriores)
    {
        if (novo.Tipo != Tipo) return null;

        var inicio = novo.DataHora - Janela;

        // +1 é o próprio evento novo, que ainda não está em "anteriores".
        var total = 1 + anteriores.Count(e =>
            e.DeviceId == novo.DeviceId &&
            e.Tipo == Tipo &&
            e.DataHora >= inicio &&
            e.DataHora <= novo.DataHora);

        if (total < Limite) return null;

        return new Alerta
        {
            DeviceId = novo.DeviceId,
            Regra = Nome,
            Gravidade = Gravidade,
            DataHora = novo.DataHora,
            Descricao = _mensagem
                .Replace("{n}", total.ToString())
                .Replace("{janela}", FormatarJanela(Janela))
        };
    }

    private static string FormatarJanela(TimeSpan janela)
        => janela.TotalMinutes >= 1 ? $"{(int)janela.TotalMinutes} min" : $"{(int)janela.TotalSeconds} s";
}
