namespace Sentinel.Core.Enums;

public enum Gravidade
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
    Critica = 4
}

public enum StatusIncidente
{
    Aberto = 1,
    EmAnalise = 2,
    Resolvido = 3
}

public enum SistemaOperacional
{
    Windows,
    Linux,
    MacOS,
    Android,
    IOS,
    Outro
}

public enum TipoDispositivo
{
    Desktop,
    Notebook,
    Servidor,
    Smartphone,
    Roteador,
    Outro
}

public enum TipoEvento
{
    LoginFalho,
    VarreduraDePortas,
    MalwareDetectado,
    AcessoForaDoHorario,
    Outro
}
