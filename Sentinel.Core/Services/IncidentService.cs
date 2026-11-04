using Sentinel.Core.Deteccao;
using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Interfaces;

namespace Sentinel.Core.Services;

public class IncidentService
{
    public const int TamanhoMaximoTitulo = 150;
    public const int TamanhoMaximoDescricao = 2000;
    public const int TamanhoMaximoNota = 1000;
    /// <summary>Justificativa de mudança de status/risco. Menor que a nota porque o sistema acrescenta um prefixo.</summary>
    public const int TamanhoMaximoJustificativa = 800;

    private readonly IIncidentRepository _repositorio;
    private readonly IMonitoramentoRepository _monitoramento;
    private readonly MotorDeDeteccao _motor;
    private readonly Func<DateTime> _agora;

    public IncidentService(
        IIncidentRepository repositorio,
        IMonitoramentoRepository monitoramento,
        MotorDeDeteccao motor,
        Func<DateTime>? agora = null)
    {
        _repositorio = repositorio;
        _monitoramento = monitoramento;
        _motor = motor;
        _agora = agora ?? (() => DateTime.Now);
    }

    /// <summary>Ciclo de vida: Aberto → Em análise → Resolvido. Um resolvido pode ser reaberto para análise.</summary>
    public static IReadOnlyList<StatusIncidente> TransicoesPermitidas(StatusIncidente atual) => atual switch
    {
        StatusIncidente.Aberto => new[] { StatusIncidente.EmAnalise, StatusIncidente.Resolvido },
        StatusIncidente.EmAnalise => new[] { StatusIncidente.Resolvido, StatusIncidente.Aberto },
        StatusIncidente.Resolvido => new[] { StatusIncidente.EmAnalise },
        _ => Array.Empty<StatusIncidente>()
    };

    public IReadOnlyList<Incident> Listar(StatusIncidente? status = null, Gravidade? risco = null, string? termo = null)
        => _repositorio.Buscar(status, risco, termo);

    /// <summary>Incidente completo, com as notas em ordem cronológica.</summary>
    public Incident Obter(int id)
    {
        var incidente = _repositorio.ObterCompleto(id)
            ?? throw new RegraDeNegocioException("Incidente não encontrado.");
        incidente.Notas = incidente.Notas.OrderBy(n => n.DataHora).ThenBy(n => n.Id).ToList();
        return incidente;
    }

    public Incident Abrir(string titulo, string descricao, Gravidade risco)
    {
        titulo = Texto(titulo, "O título", TamanhoMaximoTitulo, quebraDeLinha: false);
        if (titulo.Length == 0) throw new RegraDeNegocioException("O título é obrigatório.");
        descricao = Texto(descricao, "A descrição", TamanhoMaximoDescricao, quebraDeLinha: true);
        ValidarRisco(risco);

        var agora = _agora();
        var incidente = new Incident
        {
            Titulo = titulo,
            Descricao = descricao,
            Risco = risco,
            Status = StatusIncidente.Aberto,
            DataAbertura = agora
        };
        incidente.Notas.Add(new IncidentNote { Texto = "Incidente aberto manualmente.", DataHora = agora });

        _repositorio.Adicionar(incidente, Array.Empty<int>());
        return incidente;
    }

    /// <summary>
    /// Transforma um alerta em incidente: herda o risco e a descrição, e já vincula os eventos
    /// que dispararam a regra (mesmo dispositivo, mesmo tipo, dentro da janela da regra).
    /// </summary>
    public Incident AbrirAPartirDoAlerta(int alertaId)
    {
        var alerta = _monitoramento.ObterAlerta(alertaId)
            ?? throw new RegraDeNegocioException("Alerta não encontrado. Ele pode ter sido apagado.");

        var existente = _repositorio.PorAlerta(alertaId);
        if (existente is not null)
            throw new RegraDeNegocioException($"Este alerta já virou o incidente #{existente.Id}.");

        var regra = _motor.Regras.OfType<RegraPorContagem>().FirstOrDefault(r => r.Nome == alerta.Regra);
        var janela = regra?.Janela ?? TimeSpan.FromMinutes(10);

        var eventoIds = _monitoramento
            .EventosDoDispositivo(alerta.DeviceId, alerta.DataHora - janela, alerta.DataHora)
            .Where(e => regra is null || e.Tipo == regra.Tipo)
            .Select(e => e.Id)
            .ToList();

        var agora = _agora();
        var titulo = $"{alerta.Regra} em {alerta.Device.Nome}";
        var incidente = new Incident
        {
            Titulo = titulo.Length <= TamanhoMaximoTitulo ? titulo : titulo[..TamanhoMaximoTitulo],
            Descricao = alerta.Descricao,
            Risco = alerta.Gravidade,
            Status = StatusIncidente.Aberto,
            DataAbertura = agora,
            AlertaOrigemId = alerta.Id
        };
        incidente.Notas.Add(new IncidentNote
        {
            Texto = $"Incidente aberto a partir do alerta #{alerta.Id} ({alerta.Regra}). {eventoIds.Count} evento(s) vinculado(s).",
            DataHora = agora
        });

        _repositorio.Adicionar(incidente, eventoIds);
        return incidente;
    }

    public void AdicionarNota(int incidenteId, string texto)
    {
        texto = Texto(texto, "A nota", TamanhoMaximoNota, quebraDeLinha: true);
        if (texto.Length == 0) throw new RegraDeNegocioException("Escreva a nota antes de adicionar.");

        var incidente = ObterOuFalhar(incidenteId);
        _repositorio.Atualizar(incidente, new IncidentNote { Texto = texto, DataHora = _agora() });
    }

    /// <summary>Muda o status respeitando o ciclo de vida. Para resolver, a justificativa é obrigatória.</summary>
    public void MudarStatus(int incidenteId, StatusIncidente novo, string? justificativa = null)
    {
        var incidente = ObterOuFalhar(incidenteId);

        if (!TransicoesPermitidas(incidente.Status).Contains(novo))
            throw new RegraDeNegocioException(
                $"Não é possível mover um incidente de \"{incidente.Status.Rotulo()}\" para \"{novo.Rotulo()}\".");

        var texto = Texto(justificativa, "A justificativa", TamanhoMaximoJustificativa, quebraDeLinha: true);
        if (novo == StatusIncidente.Resolvido && texto.Length == 0)
            throw new RegraDeNegocioException("Para resolver um incidente, registre na nota o que foi feito.");

        var anterior = incidente.Status;
        incidente.Status = novo;
        incidente.DataResolucao = novo == StatusIncidente.Resolvido ? _agora() : null;

        _repositorio.Atualizar(incidente, new IncidentNote
        {
            Texto = Auditoria($"Status alterado de {anterior.Rotulo()} para {novo.Rotulo()}.", texto),
            DataHora = _agora()
        });
    }

    public void AlterarRisco(int incidenteId, Gravidade novo, string? justificativa = null)
    {
        ValidarRisco(novo);
        var incidente = ObterOuFalhar(incidenteId);

        if (incidente.Risco == novo)
            throw new RegraDeNegocioException($"O risco já é \"{novo.Rotulo()}\".");

        var texto = Texto(justificativa, "A justificativa", TamanhoMaximoJustificativa, quebraDeLinha: true);
        var anterior = incidente.Risco;
        incidente.Risco = novo;

        _repositorio.Atualizar(incidente, new IncidentNote
        {
            Texto = Auditoria($"Risco alterado de {anterior.Rotulo()} para {novo.Rotulo()}.", texto),
            DataHora = _agora()
        });
    }

    private Incident ObterOuFalhar(int id)
        => _repositorio.ObterCompleto(id) ?? throw new RegraDeNegocioException("Incidente não encontrado.");

    private static string Auditoria(string acao, string justificativa)
        => justificativa.Length == 0 ? acao : $"{acao} {justificativa}";

    private static void ValidarRisco(Gravidade risco)
    {
        if (!Enum.IsDefined(risco)) throw new RegraDeNegocioException("Risco inválido.");
    }

    private static string Texto(string? texto, string campo, int maximo, bool quebraDeLinha)
    {
        var limpo = texto?.Trim() ?? string.Empty;
        if (limpo.Length > maximo)
            throw new RegraDeNegocioException($"{campo} deve ter no máximo {maximo} caracteres.");
        if (Validacao.TemCaractereDeControle(limpo, quebraDeLinha))
            throw new RegraDeNegocioException($"{campo} contém caracteres de controle não permitidos.");
        return limpo;
    }
}
