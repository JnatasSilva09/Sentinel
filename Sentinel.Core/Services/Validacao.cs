namespace Sentinel.Core.Services;

public static class Validacao
{
    /// <summary>
    /// True se o texto tem caracteres de controle (nulo, escape, etc.). Em campos de várias linhas,
    /// passe permitirQuebraDeLinha para aceitar \n, \r e \t.
    /// </summary>
    public static bool TemCaractereDeControle(string texto, bool permitirQuebraDeLinha = false)
        => texto.Any(c => char.IsControl(c) && !(permitirQuebraDeLinha && c is '\n' or '\r' or '\t'));
}
