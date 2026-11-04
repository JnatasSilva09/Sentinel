using Sentinel.Core.Entities;
using Sentinel.Core.Enums;

namespace Sentinel.Core.Interfaces;

public interface IDeviceRepository
{
    IReadOnlyList<Device> Buscar(string? termo, SistemaOperacional? sistemaOperacional);
    Device? ObterPorId(int id);
    bool IpExiste(string enderecoIp, int? ignorarId);
    void Adicionar(Device device);
    void Atualizar(Device device);
    bool Remover(int id);
}
