namespace Sentinel.Data;

/// <summary>Monta padrões de LIKE seguros: o que o usuário digita vale como texto, não como curinga.</summary>
internal static class SqlLike
{
    public const string Escape = "\\";

    public static string Contem(string termo)
        => "%" + termo.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
