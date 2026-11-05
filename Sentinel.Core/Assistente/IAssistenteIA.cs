namespace Sentinel.Core.Assistente;

/// <summary>
/// Assistente de análise de segurança. Tem uma implementação offline (regras e texto prontos) e uma
/// que usa o Claude. O restante do sistema só conhece esta interface.
/// Todas as respostas são SUGESTÕES: quem decide e confirma é sempre a pessoa.
/// </summary>
public interface IAssistenteIA
{
    /// <summary>Nome para mostrar na tela, por exemplo "Modo offline" ou "Claude (modelo)".</summary>
    string Nome { get; }

    Task<AnaliseAlerta> ExplicarAlertaAsync(ContextoAlerta contexto, CancellationToken ct = default);

    Task<ResumoIncidente> ResumirIncidenteAsync(ContextoIncidente contexto, CancellationToken ct = default);

    Task<ClassificacaoSugerida> SugerirGravidadeAsync(ContextoClassificacao contexto, CancellationToken ct = default);
}
