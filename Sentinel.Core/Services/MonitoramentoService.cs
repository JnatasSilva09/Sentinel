using Sentinel.Core.Deteccao;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Simulacao;

namespace Sentinel.Core.Services;

public sealed record ResultadoRegistro(SecurityEvent Evento, IReadOnlyList<Alerta> Alertas);

public class MonitoramentoService
{
    public const int TamanhoMaximoDescricao = 500;
    public const int MaximoPorRajada = 50;

    private readonly IMonitoramentoRepository _repositorio;
    private readonly IDeviceRepository _dispositivos;
    private readonly SimuladorDeEventos _simulador;
    private readonly MotorDeDeteccao _motor;
    private readonly Func<DateTime> _agora;

    public MonitoramentoService(
        IMonitoramentoRepository repositorio,
        IDeviceRepository dispositivos,
        SimuladorDeEventos simulador,
        MotorDeDeteccao motor,
        Func<DateTime>? agora = null)
    {
        _repositorio = repositorio;
        _dispositivos = dispositivos;
        _simulador = simulador;
        _motor = motor;
        _agora = agora ?? (() => DateTime.Now);
    }

    /// <summary>Valida, grava o evento, roda as regras de detecção e grava os alertas junto, de forma atômica.</summary>
    public ResultadoRegistro Registrar(SecurityEvent evento)
    {
        evento.Descricao = evento.Descricao?.Trim() ?? string.Empty;
        if (evento.Descricao.Length == 0)
            throw new RegraDeNegocioException("A descrição do evento é obrigatória.");
        if (evento.Descricao.Length > TamanhoMaximoDescricao)
            throw new RegraDeNegocioException($"A descrição deve ter no máximo {TamanhoMaximoDescricao} caracteres.");
        if (evento.Descricao.Any(char.IsControl))
            throw new RegraDeNegocioException("A descrição não pode conter caracteres de controle.");
        if (!Enum.IsDefined(evento.Tipo) || !Enum.IsDefined(evento.Gravidade))
            throw new RegraDeNegocioException("Tipo ou gravidade do evento inválidos.");
        if (_dispositivos.ObterPorId(evento.DeviceId) is null)
            throw new RegraDeNegocioException("Dispositivo não encontrado. Ele pode ter sido removido.");

        var desde = evento.DataHora - _motor.JanelaMaxima;
        var anteriores = _repositorio.EventosDoDispositivo(evento.DeviceId, desde, evento.DataHora);
        var alertasRecentes = _repositorio.AlertasDoDispositivo(evento.DeviceId, desde);

        var alertas = _motor.Avaliar(evento, anteriores, alertasRecentes);
        _repositorio.Registrar(evento, alertas);

        return new ResultadoRegistro(evento, alertas);
    }

    /// <summary>Gera e registra um evento aleatório em um dispositivo aleatório.</summary>
    public ResultadoRegistro SimularEvento()
    {
        var dispositivos = _dispositivos.Buscar(null, null);
        return Registrar(_simulador.GerarAleatorio(dispositivos, _agora()));
    }

    /// <summary>
    /// Gera uma sequência de eventos do mesmo tipo, igualmente espaçados, terminando agora.
    /// Útil para demonstrar uma regra, como 6 logins falhos em 50 segundos.
    /// </summary>
    public IReadOnlyList<ResultadoRegistro> SimularRajada(
        TipoEvento tipo, int quantidade, int intervaloSegundos, int? deviceId = null)
    {
        if (quantidade is < 1 or > MaximoPorRajada)
            throw new RegraDeNegocioException($"A quantidade deve ficar entre 1 e {MaximoPorRajada}.");
        if (intervaloSegundos is < 0 or > 3600)
            throw new RegraDeNegocioException("O intervalo deve ficar entre 0 e 3600 segundos.");

        var device = EscolherDispositivo(deviceId);
        var fim = _agora();
        var inicio = fim - TimeSpan.FromSeconds((quantidade - 1) * intervaloSegundos);

        var resultados = new List<ResultadoRegistro>(quantidade);
        for (var i = 0; i < quantidade; i++)
        {
            var quando = inicio + TimeSpan.FromSeconds(i * intervaloSegundos);
            resultados.Add(Registrar(_simulador.Gerar(device, tipo, quando)));
        }
        return resultados;
    }

    public IReadOnlyList<SecurityEvent> UltimosEventos(int quantidade = 200)
        => _repositorio.UltimosEventos(Math.Clamp(quantidade, 1, 1000));

    public IReadOnlyList<Alerta> UltimosAlertas(int quantidade = 100)
        => _repositorio.UltimosAlertas(Math.Clamp(quantidade, 1, 1000));

    public void LimparDadosSimulados() => _repositorio.LimparEventosEAlertas();

    private Device EscolherDispositivo(int? deviceId)
    {
        if (deviceId.HasValue)
            return _dispositivos.ObterPorId(deviceId.Value)
                ?? throw new RegraDeNegocioException("Dispositivo não encontrado. Ele pode ter sido removido.");

        var todos = _dispositivos.Buscar(null, null);
        if (todos.Count == 0)
            throw new RegraDeNegocioException("Cadastre pelo menos um dispositivo antes de simular eventos.");

        return todos[Random.Shared.Next(todos.Count)];
    }
}
