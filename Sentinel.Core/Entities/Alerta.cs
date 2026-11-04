using Sentinel.Core.Enums;

namespace Sentinel.Core.Entities;

/// <summary>Alerta gerado por uma regra de detecção quando um padrão suspeito aparece nos eventos.</summary>
public class Alerta
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string Regra { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public Gravidade Gravidade { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
}
