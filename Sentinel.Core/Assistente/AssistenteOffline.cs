using System.Text;
using Sentinel.Core.Enums;
using Sentinel.Core.Simulacao;

namespace Sentinel.Core.Assistente;

/// <summary>
/// Assistente que funciona sem internet e sem chave: usa uma base de conhecimento escrita à mão e
/// regras simples de contexto. É o modo padrão e também a rede de segurança quando o Claude falha.
/// </summary>
public sealed class AssistenteOffline : IAssistenteIA
{
    public const string NomeDoModo = "Modo offline";

    public string Nome => NomeDoModo;

    public Task<AnaliseAlerta> ExplicarAlertaAsync(ContextoAlerta contexto, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var conhecimento = BaseDeConhecimento.Obter(contexto.Regra);
        var risco = contexto.Gravidade;

        var texto = new StringBuilder(conhecimento.Explicacao);
        texto.Append($" O alerta foi gerado em \"{contexto.Dispositivo.Nome}\" ({Rotulo(contexto.Dispositivo)}).");

        if (contexto.Eventos.Count > 0)
        {
            var primeiro = contexto.Eventos.Min(e => e.DataHora);
            var ultimo = contexto.Eventos.Max(e => e.DataHora);
            texto.Append($" Foram observados {contexto.Eventos.Count} evento(s) recente(s) nesse dispositivo, entre {primeiro:HH:mm:ss} e {ultimo:HH:mm:ss}.");
        }

        if (EhAtivoCritico(contexto.Dispositivo) && risco < Gravidade.Critica)
        {
            risco = Subir(risco);
            texto.Append($" Como o dispositivo é do tipo {contexto.Dispositivo.Tipo.ToString().ToLowerInvariant()} (ativo crítico: concentra serviços ou dados), " +
                         $"sugiro elevar o risco de {contexto.Gravidade.Rotulo()} para {risco.Rotulo()}.");
        }

        return Task.FromResult(new AnaliseAlerta(texto.ToString(), conhecimento.Medidas, risco, Nome));
    }

    public Task<ResumoIncidente> ResumirIncidenteAsync(ContextoIncidente c, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var texto = new StringBuilder();
        texto.Append($"Incidente #{c.Id} \"{c.Titulo}\": risco {c.Risco.Rotulo()}, status {c.Status.Rotulo()}, aberto em {c.DataAbertura:dd/MM/yyyy HH:mm}");
        if (c.DataResolucao is { } resolvido) texto.Append($" e resolvido em {resolvido:dd/MM/yyyy HH:mm}");
        texto.Append('.');

        if (c.TotalDeEventos > 0 && c.Eventos.Count > 0)
        {
            var porTipo = c.Eventos
                .GroupBy(e => e.Tipo)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key.Rotulo()} ({g.Count()})");
            var primeiro = c.Eventos.Min(e => e.DataHora);
            var ultimo = c.Eventos.Max(e => e.DataHora);

            texto.Append($" Envolve {c.TotalDeEventos} evento(s) — {string.Join(", ", porTipo)} — em {c.Dispositivos.Count} dispositivo(s) ({string.Join(", ", c.Dispositivos)}), entre {primeiro:HH:mm:ss} e {ultimo:HH:mm:ss}.");
        }
        else
        {
            texto.Append(" Não há eventos vinculados.");
        }

        if (c.Notas.Count > 0)
        {
            var ultima = c.Notas.OrderBy(n => n.DataHora).Last();
            texto.Append($" O histórico tem {c.TotalDeNotas} registro(s); o último, de {ultima.DataHora:dd/MM HH:mm}, diz: \"{ultima.Texto}\".");
        }

        texto.Append(' ').Append(ProximoPasso(c.Status));
        return Task.FromResult(new ResumoIncidente(texto.ToString(), Nome));
    }

    public Task<ClassificacaoSugerida> SugerirGravidadeAsync(ContextoClassificacao c, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var gravidade = SimuladorDeEventos.GravidadeBase(c.Evento.Tipo);
        var fatores = new List<string> { $"eventos do tipo \"{c.Evento.Tipo.Rotulo()}\" costumam ser {gravidade.Rotulo()}" };

        if (c.OcorrenciasRecentesDoMesmoTipo >= 2)
        {
            gravidade = Subir(gravidade);
            fatores.Add($"houve {c.OcorrenciasRecentesDoMesmoTipo} outro(s) evento(s) do mesmo tipo nos últimos {c.JanelaEmMinutos} min, o que indica repetição");
        }

        if (EhAtivoCritico(c.Dispositivo))
        {
            gravidade = Subir(gravidade);
            fatores.Add($"o dispositivo é do tipo {c.Dispositivo.Tipo.ToString().ToLowerInvariant()}, um ativo crítico");
        }

        var justificativa = $"Gravidade sugerida: {gravidade.Rotulo()}. Fatores considerados: {string.Join("; ", fatores)}.";
        return Task.FromResult(new ClassificacaoSugerida(gravidade, justificativa, Nome));
    }

    private static bool EhAtivoCritico(ContextoDispositivo d) => d.Tipo is TipoDispositivo.Servidor or TipoDispositivo.Roteador;

    private static Gravidade Subir(Gravidade g) => g >= Gravidade.Critica ? g : g + 1;

    private static string Rotulo(ContextoDispositivo d) => $"{d.Tipo}, {d.SistemaOperacional}";

    private static string ProximoPasso(StatusIncidente status) => status switch
    {
        StatusIncidente.Aberto => "Próximo passo: iniciar a análise e conferir a origem dos eventos.",
        StatusIncidente.EmAnalise => "Próximo passo: concluir a análise e registrar o que foi feito antes de resolver.",
        _ => "Nenhuma ação pendente; considere documentar o que foi aprendido para evitar que se repita."
    };
}
