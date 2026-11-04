using Sentinel.Core.Enums;

namespace Sentinel.Core.Entities;

public class SecurityEvent
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public TipoEvento Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public Gravidade Gravidade { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
}
