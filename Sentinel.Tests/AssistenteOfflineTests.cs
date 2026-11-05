using Sentinel.Core.Assistente;
using Sentinel.Core.Deteccao;
using Sentinel.Core.Enums;

namespace Sentinel.Tests;

public class AssistenteOfflineTests
{
    private readonly AssistenteOffline _ia = new();

    [Theory]
    [InlineData(MotorDeDeteccao.RegraForcaBruta)]
    [InlineData(MotorDeDeteccao.RegraVarredura)]
    [InlineData(MotorDeDeteccao.RegraForaDoHorario)]
    [InlineData(MotorDeDeteccao.RegraMalware)]
    [InlineData("Regra que não existe")]
    public async Task Explica_qualquer_regra_com_texto_e_medidas(string regra)
    {
        var r = await _ia.ExplicarAlertaAsync(Modelos.Alerta(regra));

        Assert.False(string.IsNullOrWhiteSpace(r.Explicacao));
        Assert.InRange(r.Medidas.Count, 3, 6);
        Assert.All(r.Medidas, m => Assert.False(string.IsNullOrWhiteSpace(m)));
        Assert.Equal(AssistenteOffline.NomeDoModo, r.Fonte);
    }

    [Fact]
    public async Task Servidor_eleva_o_risco_sugerido_em_um_nivel()
    {
        var comum = await _ia.ExplicarAlertaAsync(Modelos.Alerta(MotorDeDeteccao.RegraForcaBruta, Modelos.Pc(TipoDispositivo.Desktop)));
        var servidor = await _ia.ExplicarAlertaAsync(Modelos.Alerta(MotorDeDeteccao.RegraForcaBruta, Modelos.Pc(TipoDispositivo.Servidor)));

        Assert.True(servidor.RiscoSugerido > comum.RiscoSugerido);
    }

    [Fact]
    public async Task Resume_o_incidente_citando_dispositivo()
    {
        var r = await _ia.ResumirIncidenteAsync(Modelos.Incidente());

        Assert.Contains("PC-A", r.Texto);
        Assert.Equal(AssistenteOffline.NomeDoModo, r.Fonte);
    }

    [Fact]
    public async Task Malware_e_pelo_menos_alta_gravidade()
    {
        var r = await _ia.SugerirGravidadeAsync(Modelos.Classificacao(TipoEvento.MalwareDetectado));

        Assert.True(r.Gravidade >= Gravidade.Alta);
        Assert.False(string.IsNullOrWhiteSpace(r.Justificativa));
    }

    [Fact]
    public async Task Repeticao_e_ativo_critico_aumentam_a_gravidade()
    {
        var simples = await _ia.SugerirGravidadeAsync(Modelos.Classificacao(TipoEvento.LoginFalho, 0));
        var repetido = await _ia.SugerirGravidadeAsync(Modelos.Classificacao(TipoEvento.LoginFalho, 5));
        var servidor = await _ia.SugerirGravidadeAsync(Modelos.Classificacao(TipoEvento.LoginFalho, 5, Modelos.Pc(TipoDispositivo.Servidor)));

        Assert.True(repetido.Gravidade > simples.Gravidade);
        Assert.True(servidor.Gravidade >= repetido.Gravidade);
    }
}

public class AssistenteComFallbackTests
{
    [Fact]
    public async Task Usa_o_principal_quando_ele_funciona()
    {
        var principal = new AssistenteFalso();
        var ia = new AssistenteComFallback(principal, new AssistenteOffline());

        var r = await ia.ExplicarAlertaAsync(Modelos.Alerta("x"));

        Assert.Equal("Falso", r.Fonte);
    }

    [Fact]
    public async Task Cai_para_o_offline_sem_vazar_detalhes_da_falha()
    {
        var principal = new AssistenteFalso { Falha = new HttpRequestException("segredo-sk-ant-123") };
        var ia = new AssistenteComFallback(principal, new AssistenteOffline());

        var a = await ia.ExplicarAlertaAsync(Modelos.Alerta(MotorDeDeteccao.RegraMalware));
        var i = await ia.ResumirIncidenteAsync(Modelos.Incidente());
        var c = await ia.SugerirGravidadeAsync(Modelos.Classificacao());

        foreach (var fonte in new[] { a.Fonte, i.Fonte, c.Fonte })
        {
            Assert.Contains("Modo offline", fonte);
            Assert.DoesNotContain("segredo", fonte);
        }
        Assert.False(string.IsNullOrWhiteSpace(a.Explicacao));
    }

    [Fact]
    public async Task Cancelamento_do_usuario_nao_cai_para_o_offline()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var principal = new AssistenteFalso { Falha = new OperationCanceledException(cts.Token) };
        var ia = new AssistenteComFallback(principal, new AssistenteOffline());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ia.ExplicarAlertaAsync(Modelos.Alerta("x"), cts.Token));
    }
}
