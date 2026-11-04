using Sentinel.Core.Dashboard;
using Sentinel.Core.Deteccao;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;
using Sentinel.Core.Simulacao;
using Sentinel.Data;

namespace Sentinel.Tests;

public class SerieTemporalTests
{
    private static readonly DateTime Fim = new(2026, 11, 3, 12, 0, 0);

    [Fact]
    public void Agrupa_nas_colunas_certas_respeitando_as_bordas()
    {
        var datas = new[]
        {
            Fim.AddHours(-3),                    // 09:00, início inclusivo: coluna 0
            Fim.AddHours(-2).AddSeconds(-1),     // 09:59:59: coluna 0
            Fim.AddHours(-2),                    // 10:00: coluna 1
            Fim,                                 // 12:00, exatamente o fim: última coluna
            Fim.AddSeconds(1),                   // depois do fim: ignorado
            Fim.AddHours(-3).AddSeconds(-1)      // antes do início: ignorado
        };

        var serie = SerieTemporal.Agrupar(datas, Fim, TimeSpan.FromHours(3), TimeSpan.FromHours(1));

        Assert.Equal(new[] { 2, 1, 1 }, serie.Select(p => p.Total));
        Assert.Equal(Fim.AddHours(-3), serie[0].Inicio);
        Assert.Equal(Fim.AddHours(-1), serie[2].Inicio);
    }

    [Fact]
    public void Sem_datas_retorna_colunas_zeradas()
    {
        var serie = SerieTemporal.Agrupar(Array.Empty<DateTime>(), Fim, TimeSpan.FromHours(24), TimeSpan.FromHours(1));
        Assert.Equal(24, serie.Count);
        Assert.All(serie, p => Assert.Equal(0, p.Total));
    }

    [Fact]
    public void Parametros_invalidos_sao_rejeitados()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SerieTemporal.Agrupar(Array.Empty<DateTime>(), Fim, TimeSpan.FromHours(1), TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SerieTemporal.Agrupar(Array.Empty<DateTime>(), Fim, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1)));
    }
}

public sealed class DashboardServiceTests : IDisposable
{
    private static readonly DateTime Agora = new(2026, 11, 3, 10, 20, 0);

    private readonly BancoDeTeste _banco = new();
    private readonly MonitoramentoService _monitoramento;
    private readonly IncidentService _incidentes;
    private readonly DashboardService _dashboard;
    private readonly Device _pcA;
    private readonly Device _pcB;

    public DashboardServiceTests()
    {
        var repoDispositivos = new DeviceRepository(_banco.Opcoes);
        var repoMonitoramento = new MonitoramentoRepository(_banco.Opcoes);
        var motor = MotorDeDeteccao.Padrao();

        var dispositivos = new DeviceService(repoDispositivos);
        _pcA = dispositivos.Cadastrar(new Device { Nome = "PC-A", EnderecoIp = "10.0.0.1" });
        _pcB = dispositivos.Cadastrar(new Device { Nome = "PC-B", EnderecoIp = "10.0.0.2" });

        _monitoramento = new MonitoramentoService(
            repoMonitoramento, repoDispositivos, new SimuladorDeEventos(new Random(1)), motor, () => Agora);
        _incidentes = new IncidentService(new IncidentRepository(_banco.Opcoes), repoMonitoramento, motor, () => Agora);
        _dashboard = new DashboardService(new DashboardRepository(_banco.Opcoes), () => Agora);
    }

    public void Dispose() => _banco.Dispose();

    private void Evento(Device d, TipoEvento tipo, Gravidade gravidade, DateTime quando)
        => _monitoramento.Registrar(new SecurityEvent
        {
            DeviceId = d.Id, Tipo = tipo, Gravidade = gravidade, DataHora = quando, Descricao = "teste"
        });

    /// <summary>Cenário: 4 eventos hoje, 1 ontem, 1 há 3 dias; 1 malware (gera 1 alerta crítico).</summary>
    private void PovoarCenario()
    {
        Evento(_pcA, TipoEvento.Outro, Gravidade.Baixa, Agora.AddMinutes(-15));                 // 10:05
        Evento(_pcA, TipoEvento.Outro, Gravidade.Baixa, Agora.AddMinutes(-14));                 // 10:06
        Evento(_pcB, TipoEvento.LoginFalho, Gravidade.Baixa, Agora.AddMinutes(-50));            // 09:30
        Evento(_pcB, TipoEvento.MalwareDetectado, Gravidade.Alta, Agora.AddMinutes(-35));       // 09:45
        Evento(_pcA, TipoEvento.VarreduraDePortas, Gravidade.Media, Agora.AddHours(-22).AddMinutes(-20)); // ontem 12:00
        Evento(_pcB, TipoEvento.Outro, Gravidade.Baixa, Agora.AddDays(-3).AddMinutes(-20));     // 31/10 10:00
    }

    [Fact]
    public void Vinte_e_quatro_horas_conta_so_o_periodo_e_alinha_a_janela_ao_relogio()
    {
        PovoarCenario();

        var r = _dashboard.Obter(PeriodoDashboard.Ultimas24Horas);

        Assert.Equal(new DateTime(2026, 11, 3, 11, 0, 0), r.Ate);
        Assert.Equal(new DateTime(2026, 11, 2, 11, 0, 0), r.Desde);
        Assert.Equal(5, r.TotalEventos);
        Assert.Equal(3, r.EventosPorGravidade[Gravidade.Baixa]);
        Assert.Equal(1, r.EventosPorGravidade[Gravidade.Media]);
        Assert.Equal(1, r.EventosPorGravidade[Gravidade.Alta]);
        Assert.Equal(0, r.EventosPorGravidade[Gravidade.Critica]);
        Assert.Equal(2, r.EventosPorTipo[TipoEvento.Outro]);
        Assert.Equal(0, r.EventosPorTipo[TipoEvento.AcessoForaDoHorario]); // todo valor do enum aparece, mesmo com zero
        Assert.Equal(2, r.TotalDispositivos);
    }

    [Fact]
    public void Alertas_do_periodo_sao_contados_por_gravidade()
    {
        PovoarCenario();

        var r = _dashboard.Obter(PeriodoDashboard.Ultimas24Horas);

        Assert.Equal(1, r.TotalAlertas);
        Assert.Equal(1, r.AlertasPorGravidade[Gravidade.Critica]);
    }

    [Fact]
    public void Ranking_ordena_por_quantidade_de_eventos()
    {
        PovoarCenario();

        var ranking = _dashboard.Obter(PeriodoDashboard.Ultimas24Horas).DispositivosMaisAtivos;

        Assert.Equal(new[] { ("PC-A", 3), ("PC-B", 2) }, ranking.Select(x => (x.Nome, x.Total)));
    }

    [Fact]
    public void Serie_de_24_horas_tem_24_colunas_e_soma_o_total()
    {
        PovoarCenario();

        var serie = _dashboard.Obter(PeriodoDashboard.Ultimas24Horas).EventosAoLongoDoTempo;

        Assert.Equal(24, serie.Count);
        Assert.Equal(5, serie.Sum(p => p.Total));
        Assert.Equal(new DateTime(2026, 11, 2, 11, 0, 0), serie[0].Inicio);
        Assert.Equal(new DateTime(2026, 11, 3, 10, 0, 0), serie[^1].Inicio);
        Assert.Equal(2, serie[^1].Total);  // 10:05 e 10:06
        Assert.Equal(2, serie[^2].Total);  // 09:30 e 09:45
    }

    [Fact]
    public void Sete_dias_inclui_o_evento_de_tres_dias_atras()
    {
        PovoarCenario();

        var r = _dashboard.Obter(PeriodoDashboard.Ultimos7Dias);

        Assert.Equal(6, r.TotalEventos);
        Assert.Equal(7, r.EventosAoLongoDoTempo.Count);
        Assert.Equal(new DateTime(2026, 10, 28), r.EventosAoLongoDoTempo[0].Inicio);
        Assert.Equal(4, r.EventosAoLongoDoTempo[^1].Total); // hoje
    }

    [Fact]
    public void Ultima_hora_usa_colunas_de_5_minutos()
    {
        PovoarCenario();

        var r = _dashboard.Obter(PeriodoDashboard.UltimaHora);

        Assert.Equal(new DateTime(2026, 11, 3, 10, 25, 0), r.Ate);
        Assert.Equal(12, r.EventosAoLongoDoTempo.Count);
        Assert.Equal(4, r.TotalEventos); // 09:30, 09:45, 10:05, 10:06
    }

    [Fact]
    public void Incidentes_em_andamento_somam_abertos_e_em_analise()
    {
        var a = _incidentes.Abrir("A", "d", Gravidade.Baixa);
        var b = _incidentes.Abrir("B", "d", Gravidade.Media);
        var c = _incidentes.Abrir("C", "d", Gravidade.Alta);
        _incidentes.MudarStatus(b.Id, StatusIncidente.EmAnalise);
        _incidentes.MudarStatus(c.Id, StatusIncidente.Resolvido, "ok");

        var r = _dashboard.Obter(PeriodoDashboard.Ultimas24Horas);

        Assert.Equal(2, r.IncidentesEmAndamento);
        Assert.Equal(1, r.IncidentesPorStatus[StatusIncidente.Aberto]);
        Assert.Equal(1, r.IncidentesPorStatus[StatusIncidente.EmAnalise]);
        Assert.Equal(1, r.IncidentesPorStatus[StatusIncidente.Resolvido]);
        Assert.NotEqual(0, a.Id);
    }

    [Fact]
    public void Banco_sem_eventos_retorna_tudo_zerado()
    {
        var r = _dashboard.Obter(PeriodoDashboard.Ultimas24Horas);

        Assert.Equal(0, r.TotalEventos);
        Assert.Equal(0, r.TotalAlertas);
        Assert.Equal(0, r.IncidentesEmAndamento);
        Assert.Empty(r.DispositivosMaisAtivos);
        Assert.All(r.EventosAoLongoDoTempo, p => Assert.Equal(0, p.Total));
    }

    [Fact]
    public void Periodo_invalido_e_rejeitado()
        => Assert.Throws<RegraDeNegocioException>(() => _dashboard.Obter((PeriodoDashboard)99));
}
