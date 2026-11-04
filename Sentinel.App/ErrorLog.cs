namespace Sentinel.App;

/// <summary>
/// Guarda os detalhes técnicos dos erros em um arquivo local, para o usuário ver só uma
/// mensagem clara na tela (sem caminhos internos nem mensagens técnicas de exceção).
/// </summary>
internal static class ErrorLog
{
    private static readonly string PastaLogs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel", "logs");

    public static string CaminhoDoArquivo => Path.Combine(PastaLogs, "sentinel.log");

    public static void Registrar(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(PastaLogs);
            File.AppendAllText(CaminhoDoArquivo,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Falha ao gravar o log nunca deve derrubar o aplicativo.
        }
    }
}
