using Sentinel.Core.Services;

namespace Sentinel.App;

/// <summary>
/// Roda uma chamada do assistente sem travar a tela: desativa os botões, mostra o cursor de espera e
/// cancela a chamada se a janela for fechada. Erros viram mensagens amigáveis (nada técnico na tela).
/// </summary>
internal sealed class ExecutorIa
{
    private readonly Form _janela;
    private readonly CancellationTokenSource _cancelamento = new();
    private bool _ocupado;

    public ExecutorIa(Form janela)
    {
        _janela = janela;
        janela.FormClosing += (_, _) => _cancelamento.Cancel();
        janela.FormClosed += (_, _) => _cancelamento.Dispose();
    }

    public async Task ExecutarAsync<T>(Func<CancellationToken, Task<T>> chamada, Action<T> mostrar, params Control[] botoes)
    {
        if (_ocupado) return;
        _ocupado = true;

        var habilitados = botoes.Select(b => b.Enabled).ToArray();
        foreach (var b in botoes) b.Enabled = false;
        var cursor = _janela.Cursor;
        _janela.Cursor = Cursors.WaitCursor;

        try
        {
            var resultado = await chamada(_cancelamento.Token);
            if (!_janela.IsDisposed && !_cancelamento.IsCancellationRequested) mostrar(resultado);
        }
        catch (OperationCanceledException)
        {
            // janela fechada: nada a mostrar
        }
        catch (RegraDeNegocioException ex)
        {
            if (!_janela.IsDisposed) Dialogos.Aviso(ex.Message);
        }
        catch (Exception ex)
        {
            if (!_janela.IsDisposed) Dialogos.Erro("Não consegui consultar o assistente. Nada foi alterado.", ex);
        }
        finally
        {
            _ocupado = false;
            if (!_janela.IsDisposed)
            {
                _janela.Cursor = cursor;
                for (var i = 0; i < botoes.Length; i++) botoes[i].Enabled = habilitados[i];
            }
        }
    }
}
