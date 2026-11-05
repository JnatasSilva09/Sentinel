using Sentinel.Core.Deteccao;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;
using Sentinel.Core.Simulacao;
using Sentinel.Data;

namespace Sentinel.Tests;

public sealed class AssistenteServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private readonly MonitoramentoService _monitoramento;
    private readonly IncidentService _incidentes;
    private readonly AssistenteService _servico;
    private readonly AssistenteFalso _ia = new();
    private readonly Device _pc;

    public AssistenteServiceTests()
    {
        var repoDispositivos = new DeviceRepository(_banco.Opcoes);
        var repoMonitoramento = new MonitoramentoRepository(_banco.Opcoes);
        var motor = MotorDeDeteccao.Padrao();
        _pc = new DeviceService(repoDispositivos).Cadastrar(new Device { Nome = "PC-A", EnderecoIp = "10.0.0.1" });

        _monitoramento = new MonitoramentoService(repoMonitoramento, repoDispositivos, new SimuladorDeEventos(new Random(1)), motor, () => Modelos.Agora);
        _incidentes = new IncidentService(new IncidentRepository(_banco.Opcoes), repoMonitoramento, motor);
        _servico = new AssistenteService(_ia, repoMonitoramento, _incidentes);
    }

    public void Dispose() => _banco.Dispose();

    [Fact]
    public async Task Alerta_vai_ao_assistente_sem_ip_e_sem_ids()
    {
        _monitoramento.SimularRajada(TipoEvento.LoginFalho, 6, 10, _pc.Id);
        var alerta = _monitoramento.UltimosAlertas().Single();

        var r = await _servico.ExplicarAlertaAsync(alerta.Id);

        Assert.Equal("explicação", r.Explicacao);
        var contexto = _ia.UltimoAlerta!;
        Assert.Equal("PC-A", contexto.Dispositivo.Nome);
        Assert.NotEmpty(contexto.Eventos);
        Assert.True(contexto.Eventos.Count <= 10);
        Assert.DoesNotContain("10.0.0.1", contexto.ToString());
    }

    [Fact]
    public async Task Alerta_inexistente_vira_mensagem_amigavel()
    {
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => _servico.ExplicarAlertaAsync(999));
        Assert.Equal(0, _ia.Chamadas);
    }

    [Fact]
    public async Task Resumo_do_incidente_leva_contagens_e_limita_eventos_e_notas()
    {
        _monitoramento.SimularRajada(TipoEvento.LoginFalho, 6, 10, _pc.Id);
        var incidente = _incidentes.AbrirAPartirDoAlerta(_monitoramento.UltimosAlertas().Single().Id);
        for (var i = 0; i < 15; i++) _incidentes.AdicionarNota(incidente.Id, $"nota {i}");

        var r = await _servico.ResumirIncidenteAsync(incidente.Id);

        Assert.Equal("resumo", r.Texto);
        var c = _ia.UltimoIncidente!;
        Assert.Equal(_incidentes.Obter(incidente.Id).Eventos.Count, c.TotalDeEventos);
        Assert.True(c.TotalDeEventos > 0);
        Assert.True(c.Eventos.Count <= 10);
        Assert.True(c.Notas.Count <= 10);
        Assert.True(c.TotalDeNotas >= 15);
        Assert.Contains("PC-A", c.Dispositivos);
    }

    [Fact]
    public async Task Classificacao_conta_repeticoes_do_mesmo_tipo_sem_contar_o_proprio_evento()
    {
        var eventos = _monitoramento.SimularRajada(TipoEvento.LoginFalho, 3, 10, _pc.Id);
        var ultimo = _monitoramento.UltimosEventos().First(e => e.Tipo == TipoEvento.LoginFalho);

        await _servico.SugerirGravidadeAsync(ultimo.Id);

        Assert.Equal(3, eventos.Count);
        Assert.Equal(2, _ia.UltimaClassificacao!.OcorrenciasRecentesDoMesmoTipo);
    }

    [Fact]
    public async Task Falha_do_assistente_nao_altera_nada()
    {
        _monitoramento.SimularRajada(TipoEvento.LoginFalho, 6, 10, _pc.Id);
        var alerta = _monitoramento.UltimosAlertas().Single();
        _ia.Falha = new InvalidOperationException("falhou");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _servico.ExplicarAlertaAsync(alerta.Id));

        Assert.Single(_monitoramento.UltimosAlertas());
    }

    [Fact]
    public void Reclassificar_evento_altera_so_a_gravidade()
    {
        _monitoramento.SimularRajada(TipoEvento.VarreduraDePortas, 1, 0, _pc.Id);
        var evento = _monitoramento.UltimosEventos().Single();
        var nova = evento.Gravidade == Gravidade.Critica ? Gravidade.Baixa : Gravidade.Critica;

        _monitoramento.ReclassificarEvento(evento.Id, nova);

        var depois = _monitoramento.UltimosEventos().Single();
        Assert.Equal(nova, depois.Gravidade);
        Assert.Equal(evento.Descricao, depois.Descricao);
        Assert.Single(_monitoramento.UltimosEventos());
    }

    [Fact]
    public void Reclassificar_rejeita_mesma_gravidade_inexistente_e_invalida()
    {
        _monitoramento.SimularRajada(TipoEvento.VarreduraDePortas, 1, 0, _pc.Id);
        var evento = _monitoramento.UltimosEventos().Single();

        Assert.Throws<RegraDeNegocioException>(() => _monitoramento.ReclassificarEvento(evento.Id, evento.Gravidade));
        Assert.Throws<RegraDeNegocioException>(() => _monitoramento.ReclassificarEvento(9999, Gravidade.Alta));
        Assert.Throws<RegraDeNegocioException>(() => _monitoramento.ReclassificarEvento(evento.Id, (Gravidade)99));
    }
}
