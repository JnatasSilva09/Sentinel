using Sentinel.Core.Entities;
using Sentinel.Core.Enums;

namespace Sentinel.App;

/// <summary>Linhas prontas para as tabelas (DataGridView lê propriedades públicas).</summary>
internal interface ILinhaComGravidade
{
    Gravidade Gravidade { get; }
}

internal sealed record LinhaEvento(int Id, DateTime DataHora, string Dispositivo, string Tipo, string Nivel, Gravidade Gravidade, string Descricao)
    : ILinhaComGravidade
{
    /// <summary>O evento precisa vir com o Device carregado.</summary>
    public static LinhaEvento De(SecurityEvent e)
        => new(e.Id, e.DataHora, e.Device.Nome, e.Tipo.Rotulo(), e.Gravidade.Rotulo(), e.Gravidade, e.Descricao);
}

internal sealed record LinhaAlerta(int Id, DateTime DataHora, string Dispositivo, string Regra, string Nivel, Gravidade Gravidade, string Descricao)
    : ILinhaComGravidade
{
    public static LinhaAlerta De(Alerta a)
        => new(a.Id, a.DataHora, a.Device.Nome, a.Regra, a.Gravidade.Rotulo(), a.Gravidade, a.Descricao);
}

internal sealed record LinhaIncidente(int Id, DateTime Abertura, string Titulo, string Nivel, string Situacao, int Eventos, Gravidade Gravidade, bool Resolvido)
    : ILinhaComGravidade
{
    public static LinhaIncidente De(Incident i)
        => new(i.Id, i.DataAbertura, i.Titulo, i.Risco.Rotulo(), i.Status.Rotulo(), i.Eventos.Count, i.Risco,
               i.Status == StatusIncidente.Resolvido);
}
