using Sentinel.Core.Enums;
using Sentinel.Core.Services;

namespace Sentinel.App;

/// <summary>Diálogo para abrir um incidente manualmente (os automáticos nascem de alertas).</summary>
public class IncidenteNovoForm : Form
{
    private readonly TextBox _txtTitulo = new() { MaxLength = IncidentService.TamanhoMaximoTitulo };
    private readonly ComboBox _cmbRisco = new() { DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true };
    private readonly TextBox _txtDescricao = new()
    {
        Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
        MaxLength = IncidentService.TamanhoMaximoDescricao
    };

    public string Titulo => _txtTitulo.Text.Trim();
    public string Descricao => _txtDescricao.Text.Trim();
    public Gravidade Risco => (Gravidade)_cmbRisco.SelectedItem!;

    public IncidenteNovoForm()
    {
        Text = "Novo incidente";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 320);

        _cmbRisco.DataSource = Enum.GetValues<Gravidade>();
        _cmbRisco.Format += (_, e) => { if (e.ListItem is Gravidade g) e.Value = g.Rotulo(); };
        _cmbRisco.SelectedItem = Gravidade.Media;

        var tabela = new TableLayoutPanel { Dock = DockStyle.Top, Height = 262, Padding = new Padding(12), ColumnCount = 2, RowCount = 3 };
        tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tabela.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tabela.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tabela.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AdicionarLinha(tabela, 0, "Título", _txtTitulo);
        AdicionarLinha(tabela, 1, "Risco", _cmbRisco);
        AdicionarLinha(tabela, 2, "Descrição", _txtDescricao);

        var btnSalvar = new Button { Text = "Abrir incidente", Location = new Point(262, 278), Size = new Size(110, 28) };
        var btnCancelar = new Button { Text = "Cancelar", Location = new Point(380, 278), Size = new Size(88, 28), DialogResult = DialogResult.Cancel };
        btnSalvar.Click += (_, _) => Salvar();

        Controls.Add(tabela);
        Controls.Add(btnSalvar);
        Controls.Add(btnCancelar);
        AcceptButton = null; // Enter deve quebrar linha na descrição, não fechar o diálogo
        CancelButton = btnCancelar;
    }

    private static void AdicionarLinha(TableLayoutPanel tabela, int linha, string rotulo, Control campo)
    {
        tabela.Controls.Add(new Label { Text = rotulo, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 3, 0, 0) }, 0, linha);
        campo.Dock = DockStyle.Fill;
        tabela.Controls.Add(campo, 1, linha);
    }

    private void Salvar()
    {
        if (Titulo.Length == 0)
        {
            Dialogos.Aviso("Dê um título ao incidente para continuar.");
            _txtTitulo.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
