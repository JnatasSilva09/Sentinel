namespace Sentinel.IA;

/// <summary>
/// Configuração da integração com o Claude. A chave vem SEMPRE de variável de ambiente, nunca do código,
/// de arquivo do projeto ou do repositório. Esta classe nunca mostra a chave (ToString mascara).
/// </summary>
public sealed class ConfiguracaoClaude
{
    public const string VariavelDaChave = "ANTHROPIC_API_KEY";
    public const string VariavelDoModelo = "SENTINEL_CLAUDE_MODEL";
    public const string VariavelSomenteOffline = "SENTINEL_IA_OFFLINE";
    public const string ModeloPadrao = "claude-haiku-5-5";
    public const string Endpoint = "https://api.anthropic.com/v1/messages";

    private readonly string? _chave;

    public ConfiguracaoClaude(string? chave, string? modelo = null)
    {
        _chave = LimparChave(chave);
        Modelo = LimparModelo(modelo) ?? ModeloPadrao;
    }

    public string Modelo { get; }

    /// <summary>True quando existe uma chave com aparência válida.</summary>
    public bool Habilitada => _chave is not null;

    /// <summary>Lê as variáveis de ambiente. SENTINEL_IA_OFFLINE=1 força o modo offline mesmo com chave.</summary>
    public static ConfiguracaoClaude DeAmbiente()
    {
        var somenteOffline = Environment.GetEnvironmentVariable(VariavelSomenteOffline) is "1" or "true" or "TRUE" or "True";
        var chave = somenteOffline ? null : Environment.GetEnvironmentVariable(VariavelDaChave);
        return new ConfiguracaoClaude(chave, Environment.GetEnvironmentVariable(VariavelDoModelo));
    }

    internal string ObterChave()
        => _chave ?? throw new InvalidOperationException("A chave da API não está configurada.");

    public override string ToString()
        => $"Claude {Modelo} (chave: {(Habilitada ? "configurada" : "ausente")})";

    // Só aceita ASCII visível (sem espaço ou caractere de controle): protege contra "injeção" em cabeçalho HTTP.
    private static string? LimparChave(string? chave)
    {
        var limpa = chave?.Trim();
        if (string.IsNullOrEmpty(limpa)) return null;
        return limpa.All(c => c is > ' ' and < (char)127) ? limpa : null;
    }

    private static string? LimparModelo(string? modelo)
    {
        var limpo = modelo?.Trim();
        if (string.IsNullOrEmpty(limpo) || limpo.Length > 100) return null;
        return limpo.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or ':') ? limpo : null;
    }
}
