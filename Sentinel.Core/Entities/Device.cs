using Sentinel.Core.Enums;

namespace Sentinel.Core.Entities;

public class Device
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string EnderecoIp { get; set; } = string.Empty;
    public SistemaOperacional SistemaOperacional { get; set; }
    public TipoDispositivo Tipo { get; set; }
    public DateTime DataCadastro { get; set; } = DateTime.Now;

    public ICollection<SecurityEvent> Eventos { get; set; } = new List<SecurityEvent>();
    public ICollection<Alerta> Alertas { get; set; } = new List<Alerta>();
}
