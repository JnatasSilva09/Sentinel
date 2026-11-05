using Sentinel.Core.Deteccao;

namespace Sentinel.Core.Assistente;

/// <summary>Explicações e medidas preventivas para cada regra de detecção, usadas no modo offline.</summary>
internal static class BaseDeConhecimento
{
    public sealed record Entrada(string Explicacao, IReadOnlyList<string> Medidas);

    private static readonly Dictionary<string, Entrada> PorRegra = new(StringComparer.OrdinalIgnoreCase)
    {
        [MotorDeDeteccao.RegraForcaBruta] = new Entrada(
            "Vários logins falhos seguidos no mesmo dispositivo indicam que alguém (ou um programa) está tentando adivinhar senhas, " +
            "técnica chamada força bruta. Em um ataque real, o invasor testa muitas senhas até acertar uma. " +
            "Também pode ser só uma senha antiga guardada em algum sistema, por isso vale confirmar a origem.",
            new[]
            {
                "Bloqueie temporariamente a conta ou o IP de origem depois de poucas falhas (bloqueio por tentativas).",
                "Ative a autenticação em dois fatores (MFA) nas contas atacadas.",
                "Exija senhas longas e únicas e troque a senha das contas que foram alvo.",
                "Confira nos logs se algum login teve sucesso logo depois das falhas: isso indicaria que a senha foi descoberta.",
                "Limite o acesso remoto (RDP, SSH) a redes confiáveis ou a uma VPN."
            }),

        [MotorDeDeteccao.RegraVarredura] = new Entrada(
            "Varreduras de portas repetidas são uma forma de reconhecimento: alguém testa quais portas e serviços do dispositivo estão " +
            "abertos para procurar uma brecha. Costuma ser a fase que antecede um ataque, embora também possa vir de ferramentas legítimas de inventário.",
            new[]
            {
                "Feche as portas e os serviços que não são necessários.",
                "Use regras de firewall para permitir só as origens que precisam acessar cada serviço.",
                "Deixe portas de administração (RDP, SSH, bancos de dados) fora da internet.",
                "Mantenha os serviços expostos atualizados, para que uma porta aberta não tenha vulnerabilidade conhecida.",
                "Acompanhe se, depois da varredura, houve tentativas de conexão ou de login vindas da mesma origem."
            }),

        [MotorDeDeteccao.RegraForaDoHorario] = new Entrada(
            "Acessos repetidos fora do horário comercial fogem do padrão normal de uso. Podem ser plantão ou trabalho remoto legítimos, " +
            "mas também são um sinal clássico de credencial roubada sendo usada quando ninguém está olhando.",
            new[]
            {
                "Confirme com o usuário se os acessos foram dele e se havia motivo para estarem fora do horário.",
                "Se não foram, troque a senha, encerre as sessões abertas e ative a autenticação em dois fatores.",
                "Revise as permissões da conta e remova o que não for necessário (princípio do menor privilégio).",
                "Defina política de horário de acesso para contas sensíveis e gere alerta quando ela for quebrada.",
                "Verifique o que foi acessado ou copiado durante esses acessos."
            }),

        [MotorDeDeteccao.RegraMalware] = new Entrada(
            "Foi detectado um arquivo malicioso (malware) no dispositivo. O antivírus pode ter bloqueado ou colocado o arquivo em quarentena, " +
            "mas é preciso descobrir como ele chegou e se algo foi executado antes da detecção.",
            new[]
            {
                "Isole o dispositivo da rede (desconecte cabo e Wi-Fi) para impedir que o problema se espalhe.",
                "Faça uma verificação completa com o antivírus atualizado antes de religá-lo na rede.",
                "Descubra a origem (e-mail, download, pen drive) e remova a mensagem ou o arquivo dos outros usuários.",
                "Troque as senhas usadas nesse dispositivo, pois podem ter sido capturadas.",
                "Se houver dúvida sobre a integridade do sistema, restaure a partir de um backup limpo.",
                "Procure o mesmo arquivo em outros dispositivos."
            })
    };

    private static readonly Entrada Generica = new(
        "Uma das regras de detecção encontrou um padrão fora do normal neste dispositivo. " +
        "Ainda não há uma explicação específica para esta regra, então vale revisar os eventos recentes para entender o que aconteceu.",
        new[]
        {
            "Revise os eventos recentes do dispositivo e a origem de cada um.",
            "Confirme com o responsável pelo dispositivo se a atividade era esperada.",
            "Registre o que foi encontrado em um incidente para manter o histórico.",
            "Aplique atualizações de segurança e o princípio do menor privilégio nas contas envolvidas."
        });

    public static Entrada Obter(string regra) => PorRegra.TryGetValue(regra, out var entrada) ? entrada : Generica;
}
