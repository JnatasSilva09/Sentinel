using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;

namespace Sentinel.Core.Simulacao;

/// <summary>
/// Gera eventos de segurança FICTÍCIOS. Não abre conexões, não varre portas e não toca em nenhuma rede:
/// apenas cria registros de texto. Os IPs "de origem" usam faixas reservadas para documentação (RFC 5737),
/// que por definição nunca pertencem a uma máquina real.
/// </summary>
public class SimuladorDeEventos
{
    private static readonly (TipoEvento Tipo, int Peso)[] Pesos =
    {
        (TipoEvento.LoginFalho, 45),
        (TipoEvento.VarreduraDePortas, 20),
        (TipoEvento.AcessoForaDoHorario, 20),
        (TipoEvento.Outro, 10),
        (TipoEvento.MalwareDetectado, 5)
    };

    private static readonly string[] Usuarios = { "admin", "administrador", "root", "jsilva", "maria.souza", "backup", "suporte" };
    private static readonly string[] Arquivos = { "fatura_outubro.pdf.exe", "update_helper.dll", "crack_setup.exe", "planilha_macro.xlsm", "driver_video.scr" };
    private static readonly string[] Faixas = { "203.0.113", "198.51.100" }; // TEST-NET-3 e TEST-NET-2

    private readonly Random _random;

    /// <param name="random">Passe um Random com semente fixa nos testes para resultados repetíveis.</param>
    public SimuladorDeEventos(Random? random = null) => _random = random ?? new Random();

    public SecurityEvent GerarAleatorio(IReadOnlyList<Device> dispositivos, DateTime quando)
    {
        if (dispositivos.Count == 0)
            throw new RegraDeNegocioException("Cadastre pelo menos um dispositivo antes de simular eventos.");

        var device = dispositivos[_random.Next(dispositivos.Count)];
        return Gerar(device, SortearTipo(), quando);
    }

    public SecurityEvent Gerar(Device device, TipoEvento tipo, DateTime quando) => new()
    {
        DeviceId = device.Id,
        Tipo = tipo,
        Gravidade = GravidadeBase(tipo),
        DataHora = quando,
        Descricao = Descrever(tipo, quando)
    };

    public static Gravidade GravidadeBase(TipoEvento tipo) => tipo switch
    {
        TipoEvento.LoginFalho => Gravidade.Baixa,
        TipoEvento.VarreduraDePortas => Gravidade.Media,
        TipoEvento.AcessoForaDoHorario => Gravidade.Media,
        TipoEvento.MalwareDetectado => Gravidade.Alta,
        _ => Gravidade.Baixa
    };

    private TipoEvento SortearTipo()
    {
        var sorteio = _random.Next(Pesos.Sum(p => p.Peso));
        foreach (var (tipo, peso) in Pesos)
        {
            if (sorteio < peso) return tipo;
            sorteio -= peso;
        }
        return TipoEvento.Outro; // inalcançável, mas o compilador exige
    }

    private string Descrever(TipoEvento tipo, DateTime quando) => tipo switch
    {
        TipoEvento.LoginFalho =>
            $"Falha de login do usuário '{Sortear(Usuarios)}' a partir de {IpDeOrigem()}.",
        TipoEvento.VarreduraDePortas =>
            $"Varredura de portas vinda de {IpDeOrigem()} ({_random.Next(20, 1025)} portas testadas).",
        TipoEvento.AcessoForaDoHorario =>
            $"Acesso do usuário '{Sortear(Usuarios)}' às {quando:HH:mm}, fora do horário comercial.",
        TipoEvento.MalwareDetectado =>
            $"Arquivo suspeito '{Sortear(Arquivos)}' detectado e colocado em quarentena.",
        _ => "Evento de segurança genérico registrado para análise."
    };

    private string Sortear(string[] opcoes) => opcoes[_random.Next(opcoes.Length)];

    private string IpDeOrigem() => $"{Sortear(Faixas)}.{_random.Next(1, 255)}";
}
