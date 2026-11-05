using System.Text;
using System.Text.Json;
using Sentinel.Core.Assistente;

namespace Sentinel.IA;

/// <summary>
/// Assistente que consulta o Claude pela API de mensagens da Anthropic. Segurança:
/// a chave só viaja no cabeçalho x-api-key (nunca no corpo, na URL ou em mensagens de erro);
/// os registros enviados são dados não confiáveis dentro de &lt;dados&gt;; a resposta é validada com rigor.
/// </summary>
public sealed class AssistenteClaude : IAssistenteIA
{
    private const string VersaoDaApi = "2023-06-01";
    private const int MaximoDeTokens = 900;
    private const long MaximoDeBytesNaResposta = 1_000_000;

    private readonly HttpClient _http;
    private readonly ConfiguracaoClaude _configuracao;

    public AssistenteClaude(HttpClient http, ConfiguracaoClaude configuracao)
    {
        if (!configuracao.Habilitada)
            throw new InvalidOperationException("Configure a chave da API para usar o Claude.");

        _http = http;
        _configuracao = configuracao;
    }

    public string Nome => $"Claude ({_configuracao.Modelo})";

    public async Task<AnaliseAlerta> ExplicarAlertaAsync(ContextoAlerta contexto, CancellationToken ct = default)
    {
        var resposta = await EnviarAsync(PromptsClaude.ParaExplicarAlerta(contexto), ct).ConfigureAwait(false);
        return InterpretadorDeRespostas.Alerta(resposta, Nome);
    }

    public async Task<ResumoIncidente> ResumirIncidenteAsync(ContextoIncidente contexto, CancellationToken ct = default)
    {
        var resposta = await EnviarAsync(PromptsClaude.ParaResumirIncidente(contexto), ct).ConfigureAwait(false);
        return InterpretadorDeRespostas.Resumo(resposta, Nome);
    }

    public async Task<ClassificacaoSugerida> SugerirGravidadeAsync(ContextoClassificacao contexto, CancellationToken ct = default)
    {
        var resposta = await EnviarAsync(PromptsClaude.ParaClassificar(contexto), ct).ConfigureAwait(false);
        return InterpretadorDeRespostas.Classificacao(resposta, Nome);
    }

    private async Task<string> EnviarAsync(string mensagemDoUsuario, CancellationToken ct)
    {
        var corpo = JsonSerializer.Serialize(new
        {
            model = _configuracao.Modelo,
            max_tokens = MaximoDeTokens,
            system = PromptsClaude.Sistema,
            messages = new[] { new { role = "user", content = mensagemDoUsuario } }
        });

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, ConfiguracaoClaude.Endpoint)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json")
        };
        requisicao.Headers.Add("x-api-key", _configuracao.ObterChave());
        requisicao.Headers.Add("anthropic-version", VersaoDaApi);

        using var resposta = await _http.SendAsync(requisicao, ct).ConfigureAwait(false);

        if (!resposta.IsSuccessStatusCode)
            throw new AssistenteIndisponivelException(DescreverErro((int)resposta.StatusCode));

        if (resposta.Content.Headers.ContentLength > MaximoDeBytesNaResposta)
            throw new AssistenteIndisponivelException("a resposta veio grande demais");

        var json = await resposta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ExtrairTexto(json);
    }

    // Mensagens fixas por código: nunca repassa o corpo do erro nem nada que possa conter a chave.
    private static string DescreverErro(int codigo) => codigo switch
    {
        400 => "a requisição foi recusada (verifique o nome do modelo)",
        401 or 403 => "a chave da API é inválida ou não tem permissão",
        404 => "modelo não encontrado",
        429 => "limite de uso da API atingido",
        >= 500 => "o serviço do Claude está instável",
        _ => $"erro {codigo} ao consultar o Claude"
    };

    private static string ExtrairTexto(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("content", out var conteudo) && conteudo.ValueKind == JsonValueKind.Array)
            {
                foreach (var bloco in conteudo.EnumerateArray())
                {
                    if (bloco.TryGetProperty("type", out var tipo) && tipo.ValueKind == JsonValueKind.String && tipo.GetString() == "text" &&
                        bloco.TryGetProperty("text", out var texto) && texto.ValueKind == JsonValueKind.String)
                    {
                        return texto.GetString() ?? string.Empty;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // cai no throw abaixo
        }

        throw new AssistenteIndisponivelException("a resposta não veio no formato esperado");
    }
}
