namespace Sentinel.App;

/// <summary>Mostra a resposta do assistente em texto simples (nunca interpreta HTML nem executa nada).</summary>
internal sealed class ResultadoIaForm : Form
{
    public ResultadoIaForm(string titulo, string fonte, string texto, string? textoDaAcao = null, Func<bool>? acao = null)
    {
        Text = titulo;
        MinimumSize = new Size(560, 380);
        Size = new Size(680, 520);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var lblFonte = new Label
        {
            Text = $"Fonte: {fonte}", Dock = DockStyle.Top, Height = 28, Padding = new Padding(8, 7, 0, 0),
            BackColor = Color.FromArgb(232, 240, 254), ForeColor = Color.FromArgb(20, 50, 110)
        };
        var lblAviso = new Label
        {
            Text = "Sugestão automática: confira antes de agir. Quem decide é você.",
            Dock = DockStyle.Bottom, Height = 24, Padding = new Padding(8, 4, 0, 0), ForeColor = Color.DimGray
        };
        var caixa = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White, Text = texto, Font = new Font("Segoe UI", 10f)
        };
        caixa.Select(0, 0);

        var barra = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var btnFechar = new Button { Text = "Fechar", AutoSize = true, DialogResult = DialogResult.Cancel };
        barra.Controls.Add(btnFechar);

        if (textoDaAcao is not null && acao is not null)
        {
            var btnAcao = new Button { Text = textoDaAcao, AutoSize = true };
            btnAcao.Click += (_, _) =>
            {
                if (acao()) { btnAcao.Enabled = false; btnAcao.Text = "Feito"; }
            };
            barra.Controls.Add(btnAcao);
        }

        var btnCopiar = new Button { Text = "Copiar", AutoSize = true };
        btnCopiar.Click += (_, _) =>
        {
            try { Clipboard.SetText(texto); btnCopiar.Text = "Copiado"; }
            catch (Exception) { btnCopiar.Text = "Não foi possível copiar"; }
        };
        barra.Controls.Add(btnCopiar);

        CancelButton = btnFechar;
        Controls.Add(caixa);
        Controls.Add(barra);
        Controls.Add(lblAviso);
        Controls.Add(lblFonte);
    }
}
