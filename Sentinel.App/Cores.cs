using Sentinel.Core.Enums;

namespace Sentinel.App;

/// <summary>Paleta única para gravidade, usada nas tabelas (tons claros) e nos gráficos (tons fortes).</summary>
internal static class Cores
{
    public static readonly Color Neutra = Color.FromArgb(70, 110, 160);

    public static Color Fundo(Gravidade g) => g switch
    {
        Gravidade.Critica => Color.FromArgb(255, 205, 205),
        Gravidade.Alta => Color.FromArgb(255, 228, 196),
        Gravidade.Media => Color.FromArgb(255, 248, 220),
        _ => Color.FromArgb(236, 246, 236)
    };

    public static Color Forte(Gravidade g) => g switch
    {
        Gravidade.Critica => Color.FromArgb(192, 57, 43),
        Gravidade.Alta => Color.FromArgb(230, 126, 34),
        Gravidade.Media => Color.FromArgb(214, 172, 40),
        _ => Color.FromArgb(76, 153, 92)
    };

    public static Color Status(StatusIncidente s) => s switch
    {
        StatusIncidente.Aberto => Forte(Gravidade.Alta),
        StatusIncidente.EmAnalise => Forte(Gravidade.Media),
        _ => Forte(Gravidade.Baixa)
    };
}
