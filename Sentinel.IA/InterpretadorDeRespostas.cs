using System.Text;
using System.Text.Json;
using Sentinel.Core.Assistente;
using Sentinel.Core.Enums;

namespace Sentinel.IA;

/// <summary>
/// Valida de forma ESTRITA o que o modelo respondeu. A resposta é tratada como entrada não confiável:
/// só passa JSON com os campos esperados, textos limpos e com tamanho limitado, e gravidade dentro do enum.
/// Qualquer desvio vira AssistenteIndisponivelException (e o sistema cai para o modo offline).
/// </summary>
internal static class InterpretadorDeRespostas
{
    private const int MaximoExplicacao = 1500;
    private const int MaximoMedida = 300;
    private const int MaximoDeMedidas = 5;
    private const int MaximoResumo = 1500;
    private const int MaximoJustificativa = 500;

    public static AnaliseAlerta Alerta(string texto, string fonte)
    {
        using var doc = Ler(texto);
        var raiz = doc.RootElement;
        return new AnaliseAlerta(
            Texto(raiz, "explicacao", MaximoExplicacao),
            Lista(raiz, "medidas"),
            LerGravidade(raiz, "risco"),
            fonte);
    }

    public static ResumoIncidente Resumo(string texto, string fonte)
    {
        using var doc = Ler(texto);
        return new ResumoIncidente(Texto(doc.RootElement, "resumo", MaximoResumo), fonte);
    }

    public static ClassificacaoSugerida Classificacao(string texto, string fonte)
    {
        using var doc = Ler(texto);
        var raiz = doc.RootElement;
        return new ClassificacaoSugerida(
            LerGravidade(raiz, "gravidade"),
            Texto(raiz, "justificativa", MaximoJustificativa),
            fonte);
    }

    // Aceita JSON puro, dentro de ```json ... ``` ou com uma frase antes/depois: pega do primeiro { ao último }.
    private static JsonDocument Ler(string texto)
    {
        var inicio = texto.IndexOf('{');
        var fim = texto.LastIndexOf('}');
        if (inicio < 0 || fim <= inicio) throw FormatoInesperado();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(texto.Substring(inicio, fim - inicio + 1));
        }
        catch (JsonException)
        {
            throw FormatoInesperado();
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            doc.Dispose();
            throw FormatoInesperado();
        }
        return doc;
    }

    private static string Texto(JsonElement raiz, string campo, int maximo)
    {
        if (!raiz.TryGetProperty(campo, out var valor) || valor.ValueKind != JsonValueKind.String)
            throw FormatoInesperado();

        var limpo = Limpar(valor.GetString(), maximo);
        if (limpo.Length == 0) throw FormatoInesperado();
        return limpo;
    }

    private static IReadOnlyList<string> Lista(JsonElement raiz, string campo)
    {
        if (!raiz.TryGetProperty(campo, out var valor) || valor.ValueKind != JsonValueKind.Array)
            throw FormatoInesperado();

        var itens = new List<string>();
        foreach (var item in valor.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var limpo = Limpar(item.GetString(), MaximoMedida);
            if (limpo.Length > 0) itens.Add(limpo);
            if (itens.Count == MaximoDeMedidas) break;
        }

        if (itens.Count == 0) throw FormatoInesperado();
        return itens;
    }

    private static Gravidade LerGravidade(JsonElement raiz, string campo)
    {
        if (!raiz.TryGetProperty(campo, out var valor) || valor.ValueKind != JsonValueKind.String)
            throw FormatoInesperado();

        // Sem acento e sem diferenciar maiúsculas: "Média", "media" e "MEDIA" valem o mesmo.
        var chave = (valor.GetString() ?? string.Empty).Trim().ToLowerInvariant()
            .Replace('é', 'e').Replace('í', 'i').Replace('á', 'a');

        return chave switch
        {
            "baixa" => Gravidade.Baixa,
            "media" => Gravidade.Media,
            "alta" => Gravidade.Alta,
            "critica" => Gravidade.Critica,
            _ => throw FormatoInesperado()
        };
    }

    /// <summary>Remove caracteres de controle (menos quebra de linha), apara e limita o tamanho.</summary>
    private static string Limpar(string? texto, int maximo)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto)
        {
            if (c == '\n') sb.Append('\n');
            else if (!char.IsControl(c)) sb.Append(c);
        }

        var limpo = sb.ToString().Trim();
        return limpo.Length <= maximo ? limpo : limpo[..(maximo - 1)].TrimEnd() + "…";
    }

    private static AssistenteIndisponivelException FormatoInesperado()
        => new("a resposta não veio no formato esperado");
}
