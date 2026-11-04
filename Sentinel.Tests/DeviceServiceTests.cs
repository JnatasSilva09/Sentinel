using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;
using Sentinel.Data;

namespace Sentinel.Tests;

/// <summary>Testa serviço + repositório reais sobre um SQLite em memória.</summary>
public sealed class DeviceServiceTests : IDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly DeviceService _servico;
    private readonly DbContextOptions<SentinelDbContext> _opcoes;

    public DeviceServiceTests()
    {
        _conexao.Open(); // o banco em memória vive enquanto a conexão estiver aberta
        _opcoes = new DbContextOptionsBuilder<SentinelDbContext>().UseSqlite(_conexao).Options;

        using (var db = new SentinelDbContext(_opcoes))
            db.Database.EnsureCreated();

        _servico = new DeviceService(new DeviceRepository(_opcoes));
    }

    public void Dispose() => _conexao.Dispose();

    private static Device Novo(string nome = "PC-01", string ip = "10.0.0.1",
        SistemaOperacional so = SistemaOperacional.Windows)
        => new() { Nome = nome, EnderecoIp = ip, SistemaOperacional = so, Tipo = TipoDispositivo.Desktop };

    [Fact]
    public void Cadastrar_grava_e_gera_id()
    {
        var d = _servico.Cadastrar(Novo());
        Assert.True(d.Id > 0);
        Assert.Equal("PC-01", _servico.ObterPorId(d.Id)!.Nome);
    }

    [Fact]
    public void Cadastrar_remove_espacos_das_pontas()
    {
        var d = _servico.Cadastrar(Novo("  PC-01  ", " 10.0.0.1 "));
        var salvo = _servico.ObterPorId(d.Id)!;
        Assert.Equal("PC-01", salvo.Nome);
        Assert.Equal("10.0.0.1", salvo.EnderecoIp);
    }

    [Fact]
    public void Cadastrar_rejeita_ip_invalido()
        => Assert.Throws<RegraDeNegocioException>(() => _servico.Cadastrar(Novo(ip: "999.1.1.1")));

    [Fact]
    public void Cadastrar_rejeita_ip_duplicado()
    {
        _servico.Cadastrar(Novo("A", "10.0.0.1"));
        Assert.Throws<RegraDeNegocioException>(() => _servico.Cadastrar(Novo("B", "10.0.0.1")));
    }

    [Fact]
    public void Atualizar_permite_manter_o_proprio_ip()
    {
        var d = _servico.Cadastrar(Novo());
        var edit = new Device { Id = d.Id, Nome = "Renomeado", EnderecoIp = "10.0.0.1",
            SistemaOperacional = SistemaOperacional.Linux, Tipo = TipoDispositivo.Servidor };

        _servico.Atualizar(edit);

        var salvo = _servico.ObterPorId(d.Id)!;
        Assert.Equal("Renomeado", salvo.Nome);
        Assert.Equal(SistemaOperacional.Linux, salvo.SistemaOperacional);
    }

    [Fact]
    public void Atualizar_preserva_data_de_cadastro()
    {
        var d = _servico.Cadastrar(Novo());
        var original = _servico.ObterPorId(d.Id)!.DataCadastro;

        _servico.Atualizar(new Device { Id = d.Id, Nome = "X", EnderecoIp = "10.0.0.1" });

        Assert.Equal(original, _servico.ObterPorId(d.Id)!.DataCadastro);
    }

    [Fact]
    public void Atualizar_rejeita_ip_de_outro_dispositivo()
    {
        _servico.Cadastrar(Novo("A", "10.0.0.1"));
        var b = _servico.Cadastrar(Novo("B", "10.0.0.2"));

        Assert.Throws<RegraDeNegocioException>(() =>
            _servico.Atualizar(new Device { Id = b.Id, Nome = "B", EnderecoIp = "10.0.0.1" }));
    }

    [Fact]
    public void Atualizar_inexistente_lanca_regra_de_negocio()
        => Assert.Throws<RegraDeNegocioException>(() =>
            _servico.Atualizar(new Device { Id = 999, Nome = "X", EnderecoIp = "10.0.0.9" }));

    [Fact]
    public void Remover_apaga_e_remover_de_novo_lanca()
    {
        var d = _servico.Cadastrar(Novo());
        _servico.Remover(d.Id);

        Assert.Null(_servico.ObterPorId(d.Id));
        Assert.Throws<RegraDeNegocioException>(() => _servico.Remover(d.Id));
    }

    [Fact]
    public void Listar_busca_por_nome_ou_ip_e_filtra_por_so()
    {
        _servico.Cadastrar(Novo("PC-Financeiro", "192.168.1.10", SistemaOperacional.Windows));
        _servico.Cadastrar(Novo("SRV-Web", "192.168.1.101", SistemaOperacional.Linux));
        _servico.Cadastrar(Novo("SRV-Arquivos", "192.168.1.100", SistemaOperacional.Linux));

        Assert.Equal(3, _servico.Listar().Count);
        Assert.Single(_servico.Listar("financeiro"));
        Assert.Single(_servico.Listar("192.168.1.100"));
        Assert.Equal(3, _servico.Listar("192.168.1.10").Count); // casa por trecho: .10, .100 e .101
        Assert.Equal(2, _servico.Listar(null, SistemaOperacional.Linux).Count);
        Assert.Single(_servico.Listar("web", SistemaOperacional.Linux));
        Assert.Empty(_servico.Listar("web", SistemaOperacional.Windows));
    }

    [Fact]
    public void Busca_trata_porcentagem_e_underscore_como_texto_literal()
    {
        _servico.Cadastrar(Novo("PC_Financeiro", "10.0.0.1"));
        _servico.Cadastrar(Novo("PCXFinanceiro", "10.0.0.2"));
        _servico.Cadastrar(Novo("Servidor", "10.0.0.3"));

        // "_" não pode funcionar como curinga de um caractere
        Assert.Equal("PC_Financeiro", Assert.Single(_servico.Listar("PC_")).Nome);
        // "%" sozinho não pode listar tudo
        Assert.Empty(_servico.Listar("%"));
    }

    [Fact]
    public void Busca_com_tentativa_de_sql_injection_nao_quebra_nem_vaza_dados()
    {
        _servico.Cadastrar(Novo("PC-01", "10.0.0.1"));

        Assert.Empty(_servico.Listar("'; DROP TABLE Devices; --"));
        Assert.Single(_servico.Listar()); // tabela continua intacta
    }

    [Fact]
    public void Indice_unico_do_banco_vira_mensagem_amigavel_mesmo_sem_passar_pelo_servico()
    {
        // Simula a corrida: duas gravações com o mesmo IP chegando direto ao repositório.
        var repo = new DeviceRepository(_opcoes);
        repo.Adicionar(Novo("A", "10.0.0.1"));

        Assert.Throws<RegraDeNegocioException>(() => repo.Adicionar(Novo("B", "10.0.0.1")));
    }

    [Fact]
    public void Listar_vem_ordenado_por_nome()
    {
        _servico.Cadastrar(Novo("Zeta", "10.0.0.1"));
        _servico.Cadastrar(Novo("Alfa", "10.0.0.2"));

        Assert.Equal(new[] { "Alfa", "Zeta" }, _servico.Listar().Select(d => d.Nome));
    }

    [Fact]
    public void Remover_dispositivo_apaga_seus_eventos_em_cascata()
    {
        var d = _servico.Cadastrar(Novo());
        using (var db = new SentinelDbContext(_opcoes))
        {
            db.Events.Add(new SecurityEvent { DeviceId = d.Id, Tipo = TipoEvento.LoginFalho, Gravidade = Gravidade.Baixa });
            db.SaveChanges();
        }

        _servico.Remover(d.Id);

        using var db2 = new SentinelDbContext(_opcoes);
        Assert.Empty(db2.Events);
    }

    [Fact]
    public void Seed_popula_dispositivos_validos_e_nao_duplica()
    {
        using (var db = new SentinelDbContext(_opcoes))
        {
            SeedData.Popular(db);
            SeedData.Popular(db); // segunda chamada deve ser ignorada
        }

        var lista = _servico.Listar();
        Assert.NotEmpty(lista);
        Assert.All(lista, d => Assert.True(IpValidator.EhIpv4Valido(d.EnderecoIp)));
        Assert.Equal(lista.Count, lista.Select(d => d.EnderecoIp).Distinct().Count());
    }
}
