namespace Sentinel.Core.Enums;

/// <summary>Textos amigáveis (com acento) para exibir enums na interface e nos relatórios.</summary>
public static class EnumExtensions
{
    public static string Rotulo(this Gravidade g) => g switch
    {
        Gravidade.Baixa => "Baixa",
        Gravidade.Media => "Média",
        Gravidade.Alta => "Alta",
        Gravidade.Critica => "Crítica",
        _ => g.ToString()
    };

    public static string Rotulo(this StatusIncidente s) => s switch
    {
        StatusIncidente.Aberto => "Aberto",
        StatusIncidente.EmAnalise => "Em análise",
        StatusIncidente.Resolvido => "Resolvido",
        _ => s.ToString()
    };

    public static string Rotulo(this TipoEvento t) => t switch
    {
        TipoEvento.LoginFalho => "Login falho",
        TipoEvento.VarreduraDePortas => "Varredura de portas",
        TipoEvento.MalwareDetectado => "Malware detectado",
        TipoEvento.AcessoForaDoHorario => "Acesso fora do horário",
        TipoEvento.Outro => "Outro",
        _ => t.ToString()
    };
}
