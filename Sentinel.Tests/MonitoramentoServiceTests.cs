using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sentinel.Core.Deteccao;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;
using Sentinel.Core.Simulacao;
using Sentinel.Data;

namespace Sentinel.Tests;

public sealed class MonitoramentoServiceTests : IDisposable
{
    private static readonly DateTime Agora = new(2026, 11, 3, 10, 0, 0);

    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly DbContextOptions<SentinelDbContext> _opcoes;
    private readonly DeviceService _dispositivos;
    private readonly MonitoramentoService _servico;
    private readonly Device _pcA;
    private readonly Device _pcB;

    public MonitoramentoServiceTests()
    {
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<SentinelDbContext>().UseSqlite(_conexao).Options;
        using (var db = new SentinelDbContext(_opcoes)) db.Database.EnsureCreated();

        var repoDispositivos = new DeviceRepository(_opcoes);
        _dispositivos = new DeviceService(repoDispositivos);
        _pcA = _dispositivos.Cadastrar(new Device { Nome = "PC-A", EnderecoIp = "10.0.0.1" });
        _pcB = _dispositivos.Cadastrar(new Device { Nome = "PC-B", EnderecoIp = "10.0.0.2" });

        _servico = CriarServico(repoDispositivos);
    }

    private MonitoramentoService CriarServico(DeviceRepository repoDispositivos)
        => new(new MonitoramentoRepository(_opcoes), repoDispositivos,
               new SimuladorDeEventos(new Random(1)), MotorDeDeteccao.Padrao(), () => Agora);

    public void Dispose() => _conexao.Dispose();

    private SecurityEvent EventoManual(int deviceId, TipoEvento tipo = TipoEvento.Outro, string descricao = "teste")
        => new() { DeviceId = deviceId, Tipo = tipo, Gravidade = Gravidade.Baixa, DataHora = Agora, Descricao = descricao };

    [Fact]
    public void Forca_bruta_gera_exatamente_um_alerta_no_quinto_login()
    {
        var resultados = _servico.SimularRajada(TipoEvento.LoginFalho, 6, 10, _pcA.Id);

        Assert.Equal(6, resultados.Count);
        Assert.All(resultados.Take(4), r => Assert.Empty(r.Alertas));
        Assert.Single(resultados[4].Alertas);
        Assert.Empty(resultados[5].Alertas); // o 6º login é o mesmo incidente, não repete o alerta

        var alerta = Assert.Single(_servico.UltimosAlertas());
        Assert.Equal(Gravidade.Alta, alerta.Gravidade);
        Assert.Equal("PC-A", alerta.Device.Nome);
        Assert.Equal(6, _servico.UltimosEventos().Count);
    }

    [Fact]
    public void Quatro_logins_nao_geram_alerta()
    {
        _servico.SimularRajada(TipoEvento.LoginFalho, 4, 10, _pcA.Id);
        Assert.Empty(_servico.UltimosAlertas());
    }

    [Fact]
    public void Repetir_a_mesma_rajada_nao_duplica_o_alerta()
    {
        _servico.SimularRajada(TipoEvento.LoginFalho, 6, 10, _pcA.Id);
        _servico.SimularRajada(TipoEvento.LoginFalho, 6, 10, _pcA.Id);

        Assert.Single(_servico.UltimosAlertas());
        Assert.Equal(12, _servico.UltimosEventos().Count);
    }

    [Fact]
    public void Eventos_espalhados_em_dispositivos_diferentes_nao_somam()
    {
        _servico.SimularRajada(TipoEvento.LoginFalho, 3, 10, _pcA.Id);
        _servico.SimularRajada(TipoEvento.LoginFalho, 3, 10, _pcB.Id);

        Assert.Empty(_servico.UltimosAlertas());
    }

    [Fact]
    public void Malware_gera_alerta_critico()
    {
        _servico.SimularRajada(TipoEvento.MalwareDetectado, 1, 0, _pcB.Id);

        var alerta = Assert.Single(_servico.UltimosAlertas());
        Assert.Equal(Gravidade.Critica, alerta.Gravidade);
        Assert.Equal("PC-B", alerta.Device.Nome);
    }

    [Fact]
    public void Registrar_preenche_o_id_do_evento()
    {
        var resultado = _servico.Registrar(EventoManual(_pcA.Id));
        Assert.True(resultado.Evento.Id > 0);
    }

    [Fact]
    public void Dispositivo_inexistente_e_rejeitado_e_nada_e_gravado()
    {
        Assert.Throws<RegraDeNegocioException>(() => _servico.Registrar(EventoManual(deviceId: 999)));
        Assert.Empty(_servico.UltimosEventos());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("linha1\nlinha2")]
    public void Descricao_invalida_e_rejeitada(string descricao)
        => Assert.Throws<RegraDeNegocioException>(() => _servico.Registrar(EventoManual(_pcA.Id, descricao: descricao)));

    [Fact]
    public void Descricao_longa_demais_e_rejeitada()
        => Assert.Throws<RegraDeNegocioException>(() =>
            _servico.Registrar(EventoManual(_pcA.Id, descricao: new string('x', MonitoramentoService.TamanhoMaximoDescricao + 1))));

    [Fact]
    public void Tipo_ou_gravidade_fora_do_enum_sao_rejeitados()
    {
        var ev = EventoManual(_pcA.Id);
        ev.Tipo = (TipoEvento)99;
        Assert.Throws<RegraDeNegocioException>(() => _servico.Registrar(ev));

        var ev2 = EventoManual(_pcA.Id);
        ev2.Gravidade = (Gravidade)0;
        Assert.Throws<RegraDeNegocioException>(() => _servico.Registrar(ev2));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(51, 10)]
    [InlineData(5, -1)]
    [InlineData(5, 3601)]
    public void Rajada_com_parametros_fora_dos_limites_e_rejeitada(int quantidade, int intervalo)
        => Assert.Throws<RegraDeNegocioException>(() =>
            _servico.SimularRajada(TipoEvento.LoginFalho, quantidade, intervalo, _pcA.Id));

    [Fact]
    public void Simular_sem_dispositivos_pede_cadastro()
    {
        _dispositivos.Remover(_pcA.Id);
        _dispositivos.Remover(_pcB.Id);

        Assert.Throws<RegraDeNegocioException>(() => _servico.SimularEvento());
        Assert.Throws<RegraDeNegocioException>(() => _servico.SimularRajada(TipoEvento.LoginFalho, 1, 0));
    }

    [Fact]
    public void Evento_aleatorio_e_gravado()
    {
        var r = _servico.SimularEvento();
        Assert.Single(_servico.UltimosEventos());
        Assert.Equal(r.Evento.Id, _servico.UltimosEventos()[0].Id);
    }

    [Fact]
    public void Ultimos_eventos_vem_do_mais_recente_para_o_mais_antigo()
    {
        _servico.SimularRajada(TipoEvento.VarreduraDePortas, 2, 30, _pcA.Id);

        var eventos = _servico.UltimosEventos();
        Assert.True(eventos[0].DataHora > eventos[1].DataHora);
    }

    [Fact]
    public void Limpar_apaga_eventos_e_alertas_mas_mantem_dispositivos()
    {
        _servico.SimularRajada(TipoEvento.MalwareDetectado, 1, 0, _pcA.Id);

        _servico.LimparDadosSimulados();

        Assert.Empty(_servico.UltimosEventos());
        Assert.Empty(_servico.UltimosAlertas());
        Assert.Equal(2, _dispositivos.Listar().Count);
    }

    [Fact]
    public void Excluir_dispositivo_apaga_seus_eventos_e_alertas_em_cascata()
    {
        _servico.SimularRajada(TipoEvento.MalwareDetectado, 1, 0, _pcA.Id);

        _dispositivos.Remover(_pcA.Id);

        Assert.Empty(_servico.UltimosEventos());
        Assert.Empty(_servico.UltimosAlertas());
    }
}
