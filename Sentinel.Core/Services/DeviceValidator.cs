using Sentinel.Core.Entities;

namespace Sentinel.Core.Services;

public static class IpValidator
{
    /// <summary>Aceita apenas IPv4 decimal estrito, com 4 partes e sem zeros à esquerda (ex.: 192.168.0.10).</summary>
    public static bool EhIpv4Valido(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;

        var partes = ip.Trim().Split('.');
        if (partes.Length != 4) return false;

        foreach (var parte in partes)
        {
            if (parte.Length is < 1 or > 3) return false;
            if (!parte.All(char.IsAsciiDigit)) return false;
            if (parte.Length > 1 && parte[0] == '0') return false; // "010" é ambíguo (octal em alguns sistemas)
            if (int.Parse(parte) > 255) return false;
        }

        return true;
    }
}

public static class DeviceValidator
{
    public const int TamanhoMaximoNome = 100;

    public static IReadOnlyList<string> Validar(Device device)
    {
        var erros = new List<string>();

        var nome = device.Nome?.Trim() ?? string.Empty;
        if (nome.Length == 0)
            erros.Add("O nome é obrigatório.");
        else if (nome.Length > TamanhoMaximoNome)
            erros.Add($"O nome deve ter no máximo {TamanhoMaximoNome} caracteres.");
        else if (nome.Any(char.IsControl))
            erros.Add("O nome não pode conter quebras de linha, tabulações ou outros caracteres de controle.");

        if (!IpValidator.EhIpv4Valido(device.EnderecoIp))
            erros.Add("Endereço IP inválido. Use o formato IPv4, por exemplo 192.168.0.10.");

        return erros;
    }
}
