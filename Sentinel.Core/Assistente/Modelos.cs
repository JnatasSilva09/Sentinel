using Sentinel.Core.Enums;

namespace Sentinel.Core.Assistente;

// Contextos: só o que o assistente precisa para analisar. De propósito NÃO levam endereço IP,
// ids internos de dispositivos nem nada além de texto curto (minimização de dados).

public sealed record ContextoDispositivo(string Nome, SistemaOperacional SistemaOperacional, TipoDispositivo Tipo);

public sealed record ContextoEvento(DateTime DataHora, TipoEvento Tipo, Gravidade Gravidade, string Descricao);

public sealed record ContextoNota(DateTime DataHora, string Texto);

public sealed record ContextoAlerta(
    int Id,
    string Regra,
    Gravidade Gravidade,
    string Descricao,
    DateTime DataHora,
    ContextoDispositivo Dispositivo,
    IReadOnlyList<ContextoEvento> Eventos);

public sealed record ContextoIncidente(
    int Id,
    string Titulo,
    string Descricao,
    Gravidade Risco,
    StatusIncidente Status,
    DateTime DataAbertura,
    DateTime? DataResolucao,
    int TotalDeEventos,
    IReadOnlyList<ContextoEvento> Eventos,
    IReadOnlyList<string> Dispositivos,
    int TotalDeNotas,
    IReadOnlyList<ContextoNota> Notas);

public sealed record ContextoClassificacao(
    ContextoEvento Evento,
    ContextoDispositivo Dispositivo,
    int OcorrenciasRecentesDoMesmoTipo,
    int JanelaEmMinutos);

// Resultados. "Fonte" diz de onde veio a resposta (modo offline ou o modelo), e a interface mostra isso.

public sealed record AnaliseAlerta(string Explicacao, IReadOnlyList<string> Medidas, Gravidade RiscoSugerido, string Fonte);

public sealed record ResumoIncidente(string Texto, string Fonte);

public sealed record ClassificacaoSugerida(Gravidade Gravidade, string Justificativa, string Fonte);

/// <summary>Assistente online fora do ar, sem chave válida, sem internet ou com resposta inutilizável.</summary>
public class AssistenteIndisponivelException : Exception
{
    public AssistenteIndisponivelException(string motivo) : base(motivo) { }
}
