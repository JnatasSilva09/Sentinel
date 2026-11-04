namespace Sentinel.Core.Dashboard;

public static class SerieTemporal
{
    /// <summary>
    /// Conta datas em colunas de tamanho fixo cobrindo [fim - duracao, fim]. A última coluna termina em
    /// <paramref name="fim"/>; datas fora do intervalo são ignoradas, e uma data exatamente em
    /// <paramref name="fim"/> entra na última coluna.
    /// </summary>
    public static IReadOnlyList<PontoSerie> Agrupar(IEnumerable<DateTime> datas, DateTime fim, TimeSpan duracao, TimeSpan intervalo)
    {
        if (intervalo <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(intervalo));
        if (duracao < intervalo) throw new ArgumentOutOfRangeException(nameof(duracao), "A duração deve comportar pelo menos uma coluna.");

        var colunas = (int)Math.Ceiling((double)duracao.Ticks / intervalo.Ticks);
        var inicio = fim - duracao;
        var totais = new int[colunas];

        foreach (var data in datas)
        {
            if (data < inicio || data > fim) continue;
            var indice = (int)((data - inicio).Ticks / intervalo.Ticks);
            totais[Math.Min(indice, colunas - 1)]++;
        }

        return Enumerable.Range(0, colunas)
            .Select(i => new PontoSerie(inicio + TimeSpan.FromTicks(intervalo.Ticks * i), totais[i]))
            .ToList();
    }
}
