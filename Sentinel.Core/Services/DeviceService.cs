using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Interfaces;

namespace Sentinel.Core.Services;

public class DeviceService
{
    private readonly IDeviceRepository _repositorio;

    public DeviceService(IDeviceRepository repositorio) => _repositorio = repositorio;

    public IReadOnlyList<Device> Listar(string? termo = null, SistemaOperacional? sistemaOperacional = null)
        => _repositorio.Buscar(termo, sistemaOperacional);

    public Device? ObterPorId(int id) => _repositorio.ObterPorId(id);

    public Device Cadastrar(Device device)
    {
        Normalizar(device);
        Validar(device, ignorarId: null);

        device.DataCadastro = DateTime.Now;
        _repositorio.Adicionar(device);
        return device;
    }

    public void Atualizar(Device device)
    {
        if (_repositorio.ObterPorId(device.Id) is null)
            throw new RegraDeNegocioException("Dispositivo não encontrado. Ele pode ter sido removido.");

        Normalizar(device);
        Validar(device, ignorarId: device.Id);
        _repositorio.Atualizar(device);
    }

    public void Remover(int id)
    {
        if (!_repositorio.Remover(id))
            throw new RegraDeNegocioException("Dispositivo não encontrado. Ele pode já ter sido removido.");
    }

    private static void Normalizar(Device device)
    {
        device.Nome = device.Nome?.Trim() ?? string.Empty;
        device.EnderecoIp = device.EnderecoIp?.Trim() ?? string.Empty;
    }

    private void Validar(Device device, int? ignorarId)
    {
        var erros = DeviceValidator.Validar(device);
        if (erros.Count > 0)
            throw new RegraDeNegocioException(string.Join(Environment.NewLine, erros));

        if (_repositorio.IpExiste(device.EnderecoIp, ignorarId))
            throw new RegraDeNegocioException($"Já existe um dispositivo cadastrado com o IP {device.EnderecoIp}.");
    }
}
