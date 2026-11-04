using Sentinel.Core.Enums;

namespace Sentinel.Core.Entities;

public class Incident
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public Gravidade Risco { get; set; }
    public StatusIncidente Status { get; set; } = StatusIncidente.Aberto;
    public DateTime DataAbertura { get; set; } = DateTime.Now;
    public DateTime? DataResolucao { get; set; }

    /// <summary>Alerta que originou o incidente (se foi aberto a partir de um). Vira null se o alerta for apagado.</summary>
    public int? AlertaOrigemId { get; set; }
    public Alerta? AlertaOrigem { get; set; }

    public ICollection<IncidentEvent> Eventos { get; set; } = new List<IncidentEvent>();
    public ICollection<IncidentNote> Notas { get; set; } = new List<IncidentNote>();
}

/// <summary>Vínculo N:N entre incidentes e eventos de segurança.</summary>
public class IncidentEvent
{
    public int IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;
    public int SecurityEventId { get; set; }
    public SecurityEvent SecurityEvent { get; set; } = null!;
}

/// <summary>Histórico de anotações de um incidente.</summary>
public class IncidentNote
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;
    public string Texto { get; set; } = string.Empty;
    public DateTime DataHora { get; set; } = DateTime.Now;
}
