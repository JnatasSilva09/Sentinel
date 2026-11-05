using Sentinel.Core.Assistente;

namespace Sentinel.IA;

public static class AssistenteFactory
{
    /// <summary>Sem chave: só o modo offline. Com chave: Claude, com o modo offline como reserva.</summary>
    public static IAssistenteIA Criar(ConfiguracaoClaude configuracao, HttpClient http)
    {
        var offline = new AssistenteOffline();
        return configuracao.Habilitada
            ? new AssistenteComFallback(new AssistenteClaude(http, configuracao), offline)
            : offline;
    }
}
