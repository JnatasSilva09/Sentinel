namespace Sentinel.Core.Assistente;

/// <summary>
/// Tenta o assistente principal (online) e, se ele falhar por qualquer motivo, responde com o de reserva (offline),
/// avisando na própria resposta o que aconteceu. O usuário nunca fica sem resposta por causa da internet ou da chave.
/// Cancelamento pedido pelo usuário NÃO cai para a reserva: é respeitado.
/// </summary>
public sealed class AssistenteComFallback : IAssistenteIA
{
    private readonly IAssistenteIA _principal;
    private readonly IAssistenteIA _reserva;

    public AssistenteComFallback(IAssistenteIA principal, IAssistenteIA reserva)
    {
        _principal = principal;
        _reserva = reserva;
    }

    public string Nome => _principal.Nome;

    public async Task<AnaliseAlerta> ExplicarAlertaAsync(ContextoAlerta contexto, CancellationToken ct = default)
    {
        try
        {
            return await _principal.ExplicarAlertaAsync(contexto, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (DeveUsarReserva(ex, ct))
        {
            var resposta = await _reserva.ExplicarAlertaAsync(contexto, ct).ConfigureAwait(false);
            return resposta with { Fonte = FonteDaReserva(ex) };
        }
    }

    public async Task<ResumoIncidente> ResumirIncidenteAsync(ContextoIncidente contexto, CancellationToken ct = default)
    {
        try
        {
            return await _principal.ResumirIncidenteAsync(contexto, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (DeveUsarReserva(ex, ct))
        {
            var resposta = await _reserva.ResumirIncidenteAsync(contexto, ct).ConfigureAwait(false);
            return resposta with { Fonte = FonteDaReserva(ex) };
        }
    }

    public async Task<ClassificacaoSugerida> SugerirGravidadeAsync(ContextoClassificacao contexto, CancellationToken ct = default)
    {
        try
        {
            return await _principal.SugerirGravidadeAsync(contexto, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (DeveUsarReserva(ex, ct))
        {
            var resposta = await _reserva.SugerirGravidadeAsync(contexto, ct).ConfigureAwait(false);
            return resposta with { Fonte = FonteDaReserva(ex) };
        }
    }

    private static bool DeveUsarReserva(Exception ex, CancellationToken ct)
        => !(ex is OperationCanceledException && ct.IsCancellationRequested);

    // Só motivos genéricos: nunca repassa texto de exceção que possa conter detalhes técnicos.
    private string FonteDaReserva(Exception ex)
    {
        var motivo = ex switch
        {
            AssistenteIndisponivelException e => e.Message,
            HttpRequestException => "sem conexão com a internet",
            OperationCanceledException => "tempo de resposta esgotado",
            _ => "resposta inesperada do serviço"
        };
        return $"{AssistenteOffline.NomeDoModo} ({_principal.Nome} indisponível: {motivo})";
    }
}
