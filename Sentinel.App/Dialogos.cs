using Sentinel.Core.Services;

namespace Sentinel.App;

/// <summary>
/// Executa ações do serviço mostrando mensagens claras: regra de negócio vira aviso;
/// erro inesperado vai para o log e o usuário vê uma mensagem amigável, sem detalhe técnico.
/// </summary>
internal static class Dialogos
{
    public static void Aviso(string mensagem)
        => MessageBox.Show(mensagem, "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Erro(string mensagem, Exception ex)
    {
        ErrorLog.Registrar(ex);
        MessageBox.Show(
            $"{mensagem}{Environment.NewLine}{Environment.NewLine}" +
            $"Os detalhes técnicos foram salvos em:{Environment.NewLine}{ErrorLog.CaminhoDoArquivo}",
            "Algo deu errado", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>Retorna true se a ação terminou sem erro.</summary>
    public static bool Tentar(Action acao, string mensagemDeErro)
    {
        try
        {
            acao();
            return true;
        }
        catch (RegraDeNegocioException ex)
        {
            Aviso(ex.Message);
        }
        catch (Exception ex)
        {
            Erro(mensagemDeErro, ex);
        }
        return false;
    }

    /// <summary>Retorna o resultado, ou null se deu erro (já mostrado ao usuário).</summary>
    public static T? TentarObter<T>(Func<T> acao, string mensagemDeErro) where T : class
    {
        try
        {
            return acao();
        }
        catch (RegraDeNegocioException ex)
        {
            Aviso(ex.Message);
        }
        catch (Exception ex)
        {
            Erro(mensagemDeErro, ex);
        }
        return null;
    }
}
