namespace Sentinel.App;

/// <summary>
/// Gráfico de barras simples desenhado com GDI+ (sem bibliotecas externas). Mostra sempre o valor
/// em número ao lado de cada barra, para não depender só de cor ou tamanho.
/// </summary>
internal sealed class GraficoBarras : Control
{
    public sealed record Item(string Rotulo, int Valor, Color Cor);

    public enum ModoGrafico { Horizontal, Vertical }

    private IReadOnlyList<Item> _itens = Array.Empty<Item>();
    private string _titulo = string.Empty;
    private ModoGrafico _modo = ModoGrafico.Horizontal;

    public GraficoBarras()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        MinimumSize = new Size(120, 90);
    }

    public string Titulo
    {
        get => _titulo;
        set { _titulo = value; Invalidate(); }
    }

    public ModoGrafico Modo
    {
        get => _modo;
        set { _modo = value; Invalidate(); }
    }

    public void Definir(IEnumerable<Item> itens)
    {
        _itens = itens.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);

        using (var borda = new Pen(Color.FromArgb(205, 205, 205)))
            g.DrawRectangle(borda, 0, 0, Width - 1, Height - 1);

        var texto = Color.FromArgb(45, 45, 45);
        using (var negrito = new Font(Font, FontStyle.Bold))
            TextRenderer.DrawText(g, _titulo, negrito, new Rectangle(8, 6, Width - 16, 20), texto,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        var area = new Rectangle(8, 30, Width - 16, Height - 38);
        if (area.Width < 40 || area.Height < 20) return;

        if (_itens.Count == 0 || _itens.All(i => i.Valor == 0))
        {
            TextRenderer.DrawText(g, "Sem dados no período", Font, area, Color.Gray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        if (_modo == ModoGrafico.Horizontal) DesenharHorizontal(g, area, texto);
        else DesenharVertical(g, area, texto);
    }

    private void DesenharHorizontal(Graphics g, Rectangle area, Color texto)
    {
        var maximo = _itens.Max(i => i.Valor);
        var altura = Math.Min(30, area.Height / _itens.Count);
        var larguraRotulo = Math.Min(area.Width / 2, _itens.Max(i => TextRenderer.MeasureText(i.Rotulo, Font).Width) + 8);
        const int larguraValor = 44;
        var larguraMaxima = Math.Max(10, area.Width - larguraRotulo - larguraValor);

        for (var i = 0; i < _itens.Count; i++)
        {
            var item = _itens[i];
            var y = area.Y + i * altura;

            TextRenderer.DrawText(g, item.Rotulo, Font, new Rectangle(area.X, y, larguraRotulo - 6, altura), texto,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var largura = (int)Math.Round((double)item.Valor / maximo * larguraMaxima);
            if (item.Valor > 0) largura = Math.Max(largura, 2);

            using (var pincel = new SolidBrush(item.Cor))
                g.FillRectangle(pincel, area.X + larguraRotulo, y + 4, largura, Math.Max(2, altura - 8));

            TextRenderer.DrawText(g, item.Valor.ToString(), Font,
                new Rectangle(area.X + larguraRotulo + largura + 4, y, larguraValor, altura), texto,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    private void DesenharVertical(Graphics g, Rectangle area, Color texto)
    {
        var maximo = _itens.Max(i => i.Valor);
        const int alturaRotulos = 18;
        const int alturaValor = 16;

        var colunas = _itens.Count;
        var larguraColuna = area.Width / (double)colunas;
        var baseY = area.Bottom - alturaRotulos;
        var topo = area.Y + alturaValor;
        var alturaMaxima = Math.Max(10, baseY - topo);

        // Mostra um rótulo a cada N colunas para os textos não se sobreporem.
        var passo = Math.Max(1, (int)Math.Ceiling(colunas * 38.0 / area.Width));

        using var fontePequena = new Font(Font.FontFamily, Math.Max(6f, Font.Size - 1));
        using (var eixo = new Pen(Color.FromArgb(180, 180, 180)))
            g.DrawLine(eixo, area.X, baseY, area.Right, baseY);

        for (var i = 0; i < colunas; i++)
        {
            var item = _itens[i];
            var x = area.X + i * larguraColuna;
            var centro = (int)Math.Round(x + larguraColuna / 2);

            var altura = (int)Math.Round((double)item.Valor / maximo * alturaMaxima);
            if (item.Valor > 0) altura = Math.Max(altura, 2);

            var larguraBarra = Math.Max(2, (int)Math.Round(larguraColuna * 0.7));
            using (var pincel = new SolidBrush(item.Cor))
                g.FillRectangle(pincel, centro - larguraBarra / 2, baseY - altura, larguraBarra, altura);

            if (item.Valor > 0 && larguraColuna >= 14)
                TextRenderer.DrawText(g, item.Valor.ToString(), fontePequena,
                    new Rectangle(centro - 20, baseY - altura - alturaValor, 40, alturaValor), texto,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (i % passo == 0)
                TextRenderer.DrawText(g, item.Rotulo, fontePequena,
                    new Rectangle(centro - 20, baseY + 2, 40, alturaRotulos), texto,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoClipping);
        }
    }
}
