using Sentinel.Core.Entities;
using Sentinel.Core.Services;

namespace Sentinel.Tests;

public class ValidacaoTests
{
    [Theory]
    [InlineData("192.168.0.10")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData(" 10.0.0.1 ")]
    public void Ip_valido(string ip) => Assert.True(IpValidator.EhIpv4Valido(ip));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("256.1.1.1")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4.5")]
    [InlineData("a.b.c.d")]
    [InlineData("1.2.3.-4")]
    [InlineData("1..2.3")]
    [InlineData("1.2.3.1000")]
    [InlineData("010.0.0.1")]
    [InlineData("192.168.001.1")]
    [InlineData("1.2.3.4; DROP TABLE Devices")]
    public void Ip_invalido(string? ip) => Assert.False(IpValidator.EhIpv4Valido(ip));

    [Fact]
    public void Nome_obrigatorio()
    {
        var erros = DeviceValidator.Validar(new Device { Nome = "  ", EnderecoIp = "10.0.0.1" });
        Assert.Single(erros);
        Assert.Contains("nome", erros[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("PC\n01")]
    [InlineData("PC\t01")]
    [InlineData("PC\u000001")]
    public void Nome_com_caracteres_de_controle_e_rejeitado(string nome)
        => Assert.Single(DeviceValidator.Validar(new Device { Nome = nome, EnderecoIp = "10.0.0.1" }));

    [Fact]
    public void Nome_muito_longo()
    {
        var device = new Device { Nome = new string('a', DeviceValidator.TamanhoMaximoNome + 1), EnderecoIp = "10.0.0.1" };
        Assert.Single(DeviceValidator.Validar(device));
    }

    [Fact]
    public void Dispositivo_valido_nao_tem_erros()
        => Assert.Empty(DeviceValidator.Validar(new Device { Nome = "PC-01", EnderecoIp = "10.0.0.1" }));
}
