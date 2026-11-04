using Sentinel.Core.Deteccao;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;
using Sentinel.Core.Simulacao;
using Sentinel.Data;

namespace Sentinel.Tests;

public sealed class IncidentServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private DateTime _agora = new(2026, 11, 3, 10, 0, 0);

    private readonly DeviceService _dispositivos;
    private readonly MonitoramentoService _monitoramento;
    private readonly IncidentService _servico;
    private readonly Device _pc;

    public IncidentServiceTests()
    {
        var repoDispositivos = new DeviceRepository(_banco.Opcoes);
        var repoMonitoramento = new MonitoramentoRepository(_banco.Opcoes);
        var motor = MotorDeDeteccao.Padrao();

        _dispositivos = new DeviceService(repoDispositivos);
        _pc = _dispositivos.Cadastrar(new Device { Nome = "PC-01", EnderecoIp = "10.0.0.1" });

        _monitoramento = new MonitoramentoService(
            repoMonitoramento, repoDispositivos, new SimuladorDeEventos(new Random(1)), motor, () => _agora);
        _servico = new IncidentService(new IncidentRepository(_banco.Opcoes), repoMonitoramento, motor, () => _agora);
    }

    public void Dispose() => _banco.Dispose();

    private Incident NovoManual(string titulo = "Incidente de teste", Gravidade risco = Gravidade.Media)
        => _servico.Abrir(titulo, "descrição", risco);

    private Alerta GerarAlertaDeForcaBruta()
    {
        _monitoramento.SimularRajada(TipoEvento.LoginFalho, 6, 10, _pc.Id);
        return Assert.Single(_monitoramento.UltimosAlertas());
    }

    // ---------- abrir manualmente ----------

    [Fact]
    public void Abrir_cria_incidente_aberto_com_nota_inicial()
    {
        var criado = _servico.Abrir("  Teste  ", "detalhes", Gravidade.Alta);

        var salvo = _servico.Obter(criado.Id);
        Assert.Equal("Teste", salvo.Titulo);
        Assert.Equal(StatusIncidente.Aberto, salvo.Status);
        Assert.Equal(Gravidade.Alta, salvo.Risco);
        Assert.Equal(_agora, salvo.DataAbertura);
        Assert.Null(salvo.DataResolucao);
        Assert.Null(salvo.AlertaOrigemId);
        Assert.Single(salvo.Notas);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("linha1\nlinha2")]
    public void Titulo_invalido_e_rejeitado(string titulo)
        => Assert.Throws<RegraDeNegocioException>(() => _servico.Abrir(titulo, "d", Gravidade.Baixa));

    [Fact]
    public void Titulo_e_descricao_longos_demais_sao_rejeitados()
    {
        Assert.Throws<RegraDeNegocioException>(() =>
            _servico.Abrir(new string('t', IncidentService.TamanhoMaximoTitulo + 1), "d", Gravidade.Baixa));
        Assert.Throws<RegraDeNegocioException>(() =>
            _servico.Abrir("ok", new string('d', IncidentService.TamanhoMaximoDescricao + 1), Gravidade.Baixa));
    }

    [Fact]
    public void Descricao_aceita_varias_linhas_mas_nao_caracteres_de_controle()
    {
        Assert.NotNull(_servico.Abrir("ok", "linha 1\r\nlinha 2\tcom tab", Gravidade.Baixa));
        Assert.Throws<RegraDeNegocioException>(() => _servico.Abrir("ok", "texto\u0000oculto", Gravidade.Baixa));
    }

    [Fact]
    public void Risco_fora_do_enum_e_rejeitado()
        => Assert.Throws<RegraDeNegocioException>(() => _servico.Abrir("ok", "d", (Gravidade)0));

    // ---------- abrir a partir de um alerta ----------

    [Fact]
    public void Alerta_vira_incidente_com_os_eventos_que_o_dispararam()
    {
        var alerta = GerarAlertaDeForcaBruta();

        var criado = _servico.AbrirAPartirDoAlerta(alerta.Id);
        var salvo = _servico.Obter(criado.Id);

        Assert.Equal(Gravidade.Alta, salvo.Risco);
        Assert.Equal(StatusIncidente.Aberto, salvo.Status);
        Assert.Equal(alerta.Id, salvo.AlertaOrigemId);
        Assert.Contains("Força bruta", salvo.Titulo);
        Assert.Contains("PC-01", salvo.Titulo);
        Assert.Equal(alerta.Descricao, salvo.Descricao);

        // são 5 os logins que dispararam a regra; o 6º chegou depois do alerta
        Assert.Equal(5, salvo.Eventos.Count);
        Assert.All(salvo.Eventos, v => Assert.Equal(TipoEvento.LoginFalho, v.SecurityEvent.Tipo));
        Assert.All(salvo.Eventos, v => Assert.Equal("PC-01", v.SecurityEvent.Device.Nome));

        var nota = Assert.Single(salvo.Notas);
        Assert.Contains($"alerta #{alerta.Id}", nota.Texto);
    }

    [Fact]
    public void Apenas_eventos_do_tipo_da_regra_sao_vinculados()
    {
        _monitoramento.Registrar(new SecurityEvent
        {
            DeviceId = _pc.Id, Tipo = TipoEvento.Outro, Gravidade = Gravidade.Baixa,
            DataHora = _agora.AddSeconds(-60), Descricao = "evento qualquer"
        });
        _monitoramento.SimularRajada(TipoEvento.MalwareDetectado, 1, 0, _pc.Id);
        var alerta = Assert.Single(_monitoramento.UltimosAlertas());

        var salvo = _servico.Obter(_servico.AbrirAPartirDoAlerta(alerta.Id).Id);

        var vinculo = Assert.Single(salvo.Eventos);
        Assert.Equal(TipoEvento.MalwareDetectado, vinculo.SecurityEvent.Tipo);
        Assert.Equal(Gravidade.Critica, salvo.Risco);
    }

    [Fact]
    public void Um_alerta_nao_vira_dois_incidentes()
    {
        var alerta = GerarAlertaDeForcaBruta();
        var primeiro = _servico.AbrirAPartirDoAlerta(alerta.Id);

        var erro = Assert.Throws<RegraDeNegocioException>(() => _servico.AbrirAPartirDoAlerta(alerta.Id));

        Assert.Contains($"#{primeiro.Id}", erro.Message);
        Assert.Single(_servico.Listar());
    }

    [Fact]
    public void Alerta_inexistente_e_rejeitado()
        => Assert.Throws<RegraDeNegocioException>(() => _servico.AbrirAPartirDoAlerta(999));

    [Fact]
    public void Limpar_dados_simulados_mantem_o_incidente_e_solta_o_vinculo()
    {
        var alerta = GerarAlertaDeForcaBruta();
        var criado = _servico.AbrirAPartirDoAlerta(alerta.Id);

        _monitoramento.LimparDadosSimulados();

        var salvo = _servico.Obter(criado.Id);
        Assert.Empty(salvo.Eventos);
        Assert.Null(salvo.AlertaOrigemId);
        Assert.Single(salvo.Notas);
    }

    // ---------- ciclo de vida ----------

    [Theory]
    [InlineData(StatusIncidente.Aberto, new[] { StatusIncidente.EmAnalise, StatusIncidente.Resolvido })]
    [InlineData(StatusIncidente.EmAnalise, new[] { StatusIncidente.Resolvido, StatusIncidente.Aberto })]
    [InlineData(StatusIncidente.Resolvido, new[] { StatusIncidente.EmAnalise })]
    public void Transicoes_permitidas(StatusIncidente atual, StatusIncidente[] esperadas)
        => Assert.Equal(esperadas, IncidentService.TransicoesPermitidas(atual));

    [Fact]
    public void Iniciar_analise_registra_a_mudanca_no_historico()
    {
        var inc = NovoManual();

        _servico.MudarStatus(inc.Id, StatusIncidente.EmAnalise);

        var salvo = _servico.Obter(inc.Id);
        Assert.Equal(StatusIncidente.EmAnalise, salvo.Status);
        Assert.Equal(2, salvo.Notas.Count);
        var ultima = salvo.Notas.Last().Texto;
        Assert.Contains("Aberto", ultima);
        Assert.Contains("Em análise", ultima);
    }

    [Fact]
    public void Resolver_exige_justificativa()
    {
        var inc = NovoManual();
        _servico.MudarStatus(inc.Id, StatusIncidente.EmAnalise);

        Assert.Throws<RegraDeNegocioException>(() => _servico.MudarStatus(inc.Id, StatusIncidente.Resolvido));
        Assert.Throws<RegraDeNegocioException>(() => _servico.MudarStatus(inc.Id, StatusIncidente.Resolvido, "   "));

        var salvo = _servico.Obter(inc.Id);
        Assert.Equal(StatusIncidente.EmAnalise, salvo.Status);
        Assert.Null(salvo.DataResolucao);
    }

    [Fact]
    public void Resolver_com_justificativa_grava_data_e_texto()
    {
        var inc = NovoManual();
        _servico.MudarStatus(inc.Id, StatusIncidente.EmAnalise);
        _agora = _agora.AddMinutes(30);

        _servico.MudarStatus(inc.Id, StatusIncidente.Resolvido, "Conta bloqueada e senha redefinida.");

        var salvo = _servico.Obter(inc.Id);
        Assert.Equal(StatusIncidente.Resolvido, salvo.Status);
        Assert.Equal(_agora, salvo.DataResolucao);
        Assert.Contains("Conta bloqueada e senha redefinida.", salvo.Notas.Last().Texto);
    }

    [Fact]
    public void Falso_positivo_pode_ser_resolvido_direto_com_justificativa()
    {
        var inc = NovoManual();
        _servico.MudarStatus(inc.Id, StatusIncidente.Resolvido, "Falso positivo: teste interno agendado.");
        Assert.Equal(StatusIncidente.Resolvido, _servico.Obter(inc.Id).Status);
    }

    [Fact]
    public void Reabrir_limpa_a_data_de_resolucao()
    {
        var inc = NovoManual();
        _servico.MudarStatus(inc.Id, StatusIncidente.Resolvido, "ok");

        _servico.MudarStatus(inc.Id, StatusIncidente.EmAnalise, "Voltou a acontecer.");

        var salvo = _servico.Obter(inc.Id);
        Assert.Equal(StatusIncidente.EmAnalise, salvo.Status);
        Assert.Null(salvo.DataResolucao);
    }

    [Fact]
    public void Transicoes_invalidas_sao_rejeitadas_e_nao_alteram_nada()
    {
        var inc = NovoManual();
        Assert.Throws<RegraDeNegocioException>(() => _servico.MudarStatus(inc.Id, StatusIncidente.Aberto));

        _servico.MudarStatus(inc.Id, StatusIncidente.Resolvido, "ok");
        Assert.Throws<RegraDeNegocioException>(() => _servico.MudarStatus(inc.Id, StatusIncidente.Aberto));
        Assert.Throws<RegraDeNegocioException>(() => _servico.MudarStatus(inc.Id, StatusIncidente.Resolvido, "de novo"));

        Assert.Equal(2, _servico.Obter(inc.Id).Notas.Count); // inicial + a única mudança válida
    }

    [Fact]
    public void Justificativa_longa_ou_com_caracteres_de_controle_e_rejeitada()
    {
        var inc = NovoManual();
        Assert.Throws<RegraDeNegocioException>(() =>
            _servico.MudarStatus(inc.Id, StatusIncidente.EmAnalise, new string('x', IncidentService.TamanhoMaximoJustificativa + 1)));
        Assert.Throws<RegraDeNegocioException>(() =>
            _servico.MudarStatus(inc.Id, StatusIncidente.EmAnalise, "ok\u0007"));
        Assert.Equal(StatusIncidente.Aberto, _servico.Obter(inc.Id).Status);
    }

    [Fact]
    public void Incidente_inexistente_e_rejeitado_em_todas_as_operacoes()
    {
        Assert.Throws<RegraDeNegocioException>(() => _servico.Obter(999));
        Assert.Throws<RegraDeNegocioException>(() => _servico.MudarStatus(999, StatusIncidente.EmAnalise));
        Assert.Throws<RegraDeNegocioException>(() => _servico.AlterarRisco(999, Gravidade.Alta));
        Assert.Throws<RegraDeNegocioException>(() => _servico.AdicionarNota(999, "x"));
    }

    // ---------- risco ----------

    [Fact]
    public void Alterar_risco_registra_a_mudanca()
    {
        var inc = NovoManual(risco: Gravidade.Baixa);

        _servico.AlterarRisco(inc.Id, Gravidade.Critica, "Atacante já acessou o servidor.");

        var salvo = _servico.Obter(inc.Id);
        Assert.Equal(Gravidade.Critica, salvo.Risco);
        var nota = salvo.Notas.Last().Texto;
        Assert.Contains("Baixa", nota);
        Assert.Contains("Crítica", nota);
        Assert.Contains("Atacante já acessou", nota);
    }

    [Fact]
    public void Alterar_para_o_mesmo_risco_ou_risco_invalido_e_rejeitado()
    {
        var inc = NovoManual(risco: Gravidade.Media);
        Assert.Throws<RegraDeNegocioException>(() => _servico.AlterarRisco(inc.Id, Gravidade.Media));
        Assert.Throws<RegraDeNegocioException>(() => _servico.AlterarRisco(inc.Id, (Gravidade)42));
    }

    // ---------- notas ----------

    [Fact]
    public void Notas_vem_em_ordem_cronologica()
    {
        var inc = NovoManual();
        _agora = _agora.AddMinutes(1);
        _servico.AdicionarNota(inc.Id, "primeira");
        _agora = _agora.AddMinutes(1);
        _servico.AdicionarNota(inc.Id, "segunda");

        var textos = _servico.Obter(inc.Id).Notas.Select(n => n.Texto).ToList();

        Assert.Equal(3, textos.Count);
        Assert.Equal("primeira", textos[1]);
        Assert.Equal("segunda", textos[2]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nota\u0000")]
    public void Nota_invalida_e_rejeitada(string texto)
    {
        var inc = NovoManual();
        Assert.Throws<RegraDeNegocioException>(() => _servico.AdicionarNota(inc.Id, texto));
    }

    [Fact]
    public void Nota_longa_demais_e_rejeitada_e_nota_com_varias_linhas_e_aceita()
    {
        var inc = NovoManual();
        Assert.Throws<RegraDeNegocioException>(() =>
            _servico.AdicionarNota(inc.Id, new string('n', IncidentService.TamanhoMaximoNota + 1)));

        _servico.AdicionarNota(inc.Id, "linha 1\r\nlinha 2");
        Assert.Contains("linha 2", _servico.Obter(inc.Id).Notas.Last().Texto);
    }

    // ---------- listagem ----------

    [Fact]
    public void Listar_filtra_por_status_risco_e_termo_do_mais_novo_para_o_mais_antigo()
    {
        var a = _servico.Abrir("Acesso suspeito", "d", Gravidade.Baixa);
        _agora = _agora.AddMinutes(1);
        var b = _servico.Abrir("Malware 100%", "d", Gravidade.Alta);
        _servico.MudarStatus(b.Id, StatusIncidente.EmAnalise);
        _agora = _agora.AddMinutes(1);
        var c = _servico.Abrir("Outro caso", "d", Gravidade.Critica);
        _servico.MudarStatus(c.Id, StatusIncidente.Resolvido, "ok");

        Assert.Equal(new[] { c.Id, b.Id, a.Id }, _servico.Listar().Select(i => i.Id));
        Assert.Equal(b.Id, Assert.Single(_servico.Listar(status: StatusIncidente.EmAnalise)).Id);
        Assert.Equal(c.Id, Assert.Single(_servico.Listar(risco: Gravidade.Critica)).Id);
        Assert.Equal(b.Id, Assert.Single(_servico.Listar(termo: "malware")).Id);   // sem diferenciar maiúsculas
        Assert.Equal(b.Id, Assert.Single(_servico.Listar(termo: "%")).Id);          // % é texto, não curinga
        Assert.Empty(_servico.Listar(termo: "_"));
        Assert.Empty(_servico.Listar(status: StatusIncidente.Aberto, risco: Gravidade.Critica));
    }

    [Fact]
    public void Listar_traz_a_contagem_de_eventos_vinculados()
    {
        var alerta = GerarAlertaDeForcaBruta();
        _servico.AbrirAPartirDoAlerta(alerta.Id);

        Assert.Equal(5, Assert.Single(_servico.Listar()).Eventos.Count);
    }
}
