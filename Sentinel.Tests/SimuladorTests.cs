using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;
using Sentinel.Core.Simulacao;

namespace Sentinel.Tests;

public class SimuladorTests
{
    private static readonly DateTime Agora = new(2026, 11, 3, 10, 0, 0);
    private static readonly List<Device> Dispositivos = new()
    {
        new Device { Id = 1, Nome = "A", EnderecoIp = "10.0.0.1" },
        new Device { Id = 2, Nome = "B", EnderecoIp = "10.0.0.2" }
    };

    [Theory]
    [InlineData(TipoEvento.LoginFalho, Gravidade.Baixa)]
    [InlineData(TipoEvento.VarreduraDePortas, Gravidade.Media)]
    [InlineData(TipoEvento.AcessoForaDoHorario, Gravidade.Media)]
    [InlineData(TipoEvento.MalwareDetectado, Gravidade.Alta)]
    [InlineData(TipoEvento.Outro, Gravidade.Baixa)]
    public void Gerar_preenche_todos_os_campos(TipoEvento tipo, Gravidade esperada)
    {
        var ev = new SimuladorDeEventos(new Random(1)).Gerar(Dispositivos[0], tipo, Agora);

        Assert.Equal(1, ev.DeviceId);
        Assert.Equal(tipo, ev.Tipo);
        Assert.Equal(esperada, ev.Gravidade);
        Assert.Equal(Agora, ev.DataHora);
        Assert.False(string.IsNullOrWhiteSpace(ev.Descricao));
    }

    [Fact]
    public void Mesma_semente_gera_a_mesma_sequencia()
    {
        var a = new SimuladorDeEventos(new Random(42));
        var b = new SimuladorDeEventos(new Random(42));

        for (var i = 0; i < 50; i++)
        {
            var ea = a.GerarAleatorio(Dispositivos, Agora);
            var eb = b.GerarAleatorio(Dispositivos, Agora);
            Assert.Equal((ea.DeviceId, ea.Tipo, ea.Descricao), (eb.DeviceId, eb.Tipo, eb.Descricao));
        }
    }

    [Fact]
    public void Distribuicao_respeita_os_pesos()
    {
        var sim = new SimuladorDeEventos(new Random(7));
        var contagem = Enumerable.Range(0, 10_000)
            .Select(_ => sim.GerarAleatorio(Dispositivos, Agora).Tipo)
            .GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        // pesos: login 45%, varredura 20%, fora do horário 20%, outro 10%, malware 5% (tolerância de 3 pontos)
        Assert.InRange(contagem[TipoEvento.LoginFalho], 4200, 4800);
        Assert.InRange(contagem[TipoEvento.VarreduraDePortas], 1700, 2300);
        Assert.InRange(contagem[TipoEvento.MalwareDetectado], 200, 800);
    }

    [Fact]
    public void Descricoes_sao_validas_e_usam_apenas_ips_de_documentacao()
    {
        var sim = new SimuladorDeEventos(new Random(3));
        for (var i = 0; i < 500; i++)
        {
            var ev = sim.GerarAleatorio(Dispositivos, Agora);
            Assert.InRange(ev.Descricao.Length, 1, 500);
            Assert.DoesNotContain(ev.Descricao, c => char.IsControl(c));
        }

        var login = sim.Gerar(Dispositivos[0], TipoEvento.LoginFalho, Agora).Descricao;
        Assert.True(login.Contains("203.0.113.") || login.Contains("198.51.100."), login);
    }

    [Fact]
    public void Sem_dispositivos_pede_cadastro()
        => Assert.Throws<RegraDeNegocioException>(() =>
            new SimuladorDeEventos().GerarAleatorio(Array.Empty<Device>(), Agora));
}
