using Sentinel.Core.Deteccao;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;

namespace Sentinel.Tests;

public class DeteccaoTests
{
    private static readonly DateTime Base = new(2026, 11, 3, 10, 0, 0);

    private static SecurityEvent Ev(TipoEvento tipo, int segundos, int deviceId = 1)
        => new() { DeviceId = deviceId, Tipo = tipo, Gravidade = Gravidade.Baixa, DataHora = Base.AddSeconds(segundos), Descricao = "teste" };

    private static IRegraDeteccao ForcaBruta()
        => MotorDeDeteccao.Padrao().Regras.Single(r => r.Nome == "Força bruta de login");

    [Fact]
    public void Quatro_logins_falhos_nao_disparam()
    {
        var anteriores = new[] { Ev(TipoEvento.LoginFalho, 0), Ev(TipoEvento.LoginFalho, 10), Ev(TipoEvento.LoginFalho, 20) };
        Assert.Null(ForcaBruta().Avaliar(Ev(TipoEvento.LoginFalho, 30), anteriores));
    }

    [Fact]
    public void Cinco_logins_falhos_em_2_minutos_disparam_alerta_alto()
    {
        var anteriores = Enumerable.Range(0, 4).Select(i => Ev(TipoEvento.LoginFalho, i * 10)).ToList();

        var alerta = ForcaBruta().Avaliar(Ev(TipoEvento.LoginFalho, 40), anteriores);

        Assert.NotNull(alerta);
        Assert.Equal(Gravidade.Alta, alerta!.Gravidade);
        Assert.Equal(1, alerta.DeviceId);
        Assert.Contains("5 tentativas", alerta.Descricao);
        Assert.Contains("2 min", alerta.Descricao);
    }

    [Fact]
    public void Eventos_fora_da_janela_nao_contam()
    {
        // um login por minuto: na janela de 2 min só cabem 3 (os de -2, -1 e 0 min)
        var anteriores = Enumerable.Range(0, 4).Select(i => Ev(TipoEvento.LoginFalho, i * 60)).ToList();
        Assert.Null(ForcaBruta().Avaliar(Ev(TipoEvento.LoginFalho, 240), anteriores));
    }

    [Fact]
    public void Evento_exatamente_na_borda_da_janela_conta()
    {
        var anteriores = new[]
        {
            Ev(TipoEvento.LoginFalho, 0), Ev(TipoEvento.LoginFalho, 30),
            Ev(TipoEvento.LoginFalho, 60), Ev(TipoEvento.LoginFalho, 90)
        };
        // o novo chega aos 120 s; a janela de 2 min começa em 0 s, que é inclusivo
        Assert.NotNull(ForcaBruta().Avaliar(Ev(TipoEvento.LoginFalho, 120), anteriores));
    }

    [Fact]
    public void Eventos_de_outro_dispositivo_ou_outro_tipo_nao_contam()
    {
        var anteriores = new[]
        {
            Ev(TipoEvento.LoginFalho, 0, deviceId: 2), Ev(TipoEvento.LoginFalho, 10, deviceId: 2),
            Ev(TipoEvento.VarreduraDePortas, 20), Ev(TipoEvento.VarreduraDePortas, 30)
        };
        Assert.Null(ForcaBruta().Avaliar(Ev(TipoEvento.LoginFalho, 40), anteriores));
    }

    [Fact]
    public void Regra_ignora_eventos_de_outro_tipo()
        => Assert.Null(ForcaBruta().Avaliar(Ev(TipoEvento.MalwareDetectado, 0), Array.Empty<SecurityEvent>()));

    [Fact]
    public void Um_malware_ja_gera_alerta_critico()
    {
        var alertas = MotorDeDeteccao.Padrao().Avaliar(
            Ev(TipoEvento.MalwareDetectado, 0), Array.Empty<SecurityEvent>(), Array.Empty<Alerta>());

        var alerta = Assert.Single(alertas);
        Assert.Equal(Gravidade.Critica, alerta.Gravidade);
    }

    [Fact]
    public void Alerta_igual_dentro_da_janela_nao_se_repete()
    {
        var jaEmitido = new Alerta { DeviceId = 1, Regra = "Malware detectado", DataHora = Base.AddMinutes(-2) };

        var alertas = MotorDeDeteccao.Padrao().Avaliar(
            Ev(TipoEvento.MalwareDetectado, 0), Array.Empty<SecurityEvent>(), new[] { jaEmitido });

        Assert.Empty(alertas);
    }

    [Fact]
    public void Alerta_antigo_demais_nao_bloqueia_um_novo()
    {
        var antigo = new Alerta { DeviceId = 1, Regra = "Malware detectado", DataHora = Base.AddMinutes(-30) };

        var alertas = MotorDeDeteccao.Padrao().Avaliar(
            Ev(TipoEvento.MalwareDetectado, 0), Array.Empty<SecurityEvent>(), new[] { antigo });

        Assert.Single(alertas);
    }

    [Fact]
    public void Alerta_de_outro_dispositivo_nao_bloqueia()
    {
        var deOutro = new Alerta { DeviceId = 2, Regra = "Malware detectado", DataHora = Base.AddMinutes(-1) };

        var alertas = MotorDeDeteccao.Padrao().Avaliar(
            Ev(TipoEvento.MalwareDetectado, 0), Array.Empty<SecurityEvent>(), new[] { deOutro });

        Assert.Single(alertas);
    }

    [Fact]
    public void Janela_maxima_do_motor_padrao_e_a_maior_das_regras()
        => Assert.Equal(TimeSpan.FromMinutes(60), MotorDeDeteccao.Padrao().JanelaMaxima);

    [Fact]
    public void Motor_sem_regras_nao_gera_alertas()
    {
        var motor = new MotorDeDeteccao(Array.Empty<IRegraDeteccao>());
        Assert.Equal(TimeSpan.Zero, motor.JanelaMaxima);
        Assert.Empty(motor.Avaliar(Ev(TipoEvento.LoginFalho, 0), Array.Empty<SecurityEvent>(), Array.Empty<Alerta>()));
    }

    [Fact]
    public void Regra_rejeita_parametros_invalidos()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RegraPorContagem("x", TipoEvento.LoginFalho, 0, TimeSpan.FromMinutes(1), Gravidade.Baixa, "m"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RegraPorContagem("x", TipoEvento.LoginFalho, 1, TimeSpan.Zero, Gravidade.Baixa, "m"));
        Assert.Throws<ArgumentException>(() =>
            new RegraPorContagem(" ", TipoEvento.LoginFalho, 1, TimeSpan.FromMinutes(1), Gravidade.Baixa, "m"));
    }
}
