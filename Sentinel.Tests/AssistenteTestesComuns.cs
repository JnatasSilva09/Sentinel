using Sentinel.Core.Assistente;
using Sentinel.Core.Enums;

namespace Sentinel.Tests;

internal static class Modelos
{
    public static readonly DateTime Agora = new(2026, 11, 3, 10, 0, 0);

   public static ContextoDispositivo Pc(TipoDispositivo tipo = TipoDispositivo.Desktop)
        => new("PC-A", SistemaOperacional.Windows, tipo);

    public static ContextoEvento Evento(TipoEvento tipo = TipoEvento.LoginFalho, string descricao = "Falha de login")
        => new(Agora, tipo, Gravidade.Baixa, descricao);

    public static ContextoAlerta Alerta(string regra, ContextoDispositivo? dispositivo = null, string descricao = "Alerta de teste")
        => new(1, regra, Gravidade.Alta, descricao, Agora, dispositivo ?? Pc(), new[] { Evento() });

    public static ContextoIncidente Incidente(string titulo = "Incidente de teste", string descricao = "Descrição")
        => new(7, titulo, descricao, Gravidade.Alta, StatusIncidente.Aberto, Agora, null, 1,
               new[] { Evento() }, new[] { "PC-A" }, 1, new[] { new ContextoNota(Agora, "nota de teste") });

    public static ContextoClassificacao Classificacao(TipoEvento tipo = TipoEvento.LoginFalho, int repeticoes = 0, ContextoDispositivo? dispositivo = null)
        => new(Evento(tipo), dispositivo ?? Pc(), repeticoes, 10);
}

internal sealed class AssistenteFalso : IAssistenteIA
{
    public Exception? Falha { get; set; }
    public int Chamadas { get; private set; }
    public ContextoAlerta? UltimoAlerta { get; private set; }
    public ContextoIncidente? UltimoIncidente { get; private set; }
    public ContextoClassificacao? UltimaClassificacao { get; private set; }

    public string Nome => "Falso";

    public Task<AnaliseAlerta> ExplicarAlertaAsync(ContextoAlerta contexto, CancellationToken ct = default)
    {
        Chamadas++;
        UltimoAlerta = contexto;
        if (Falha is not null) throw Falha;
        return Task.FromResult(new AnaliseAlerta("explicação", new[] { "medida" }, Gravidade.Alta, Nome));
    }

    public Task<ResumoIncidente> ResumirIncidenteAsync(ContextoIncidente contexto, CancellationToken ct = default)
    {
        Chamadas++;
        UltimoIncidente = contexto;
        if (Falha is not null) throw Falha;
        return Task.FromResult(new ResumoIncidente("resumo", Nome));
    }

    public Task<ClassificacaoSugerida> SugerirGravidadeAsync(ContextoClassificacao contexto, CancellationToken ct = default)
    {
        Chamadas++;
        UltimaClassificacao = contexto;
        if (Falha is not null) throw Falha;
        return Task.FromResult(new ClassificacaoSugerida(Gravidade.Alta, "justificativa", Nome));
    }
}
