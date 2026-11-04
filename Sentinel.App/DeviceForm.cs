using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;

namespace Sentinel.App;

/// <summary>Diálogo de cadastro/edição. Trabalha em uma cópia, então Cancelar não altera nada.</summary>
public class DeviceForm : Form
{
    private readonly TextBox _txtNome = new() { MaxLength = DeviceValidator.TamanhoMaximoNome };
    private readonly TextBox _txtIp = new() { MaxLength = 15, PlaceholderText = "192.168.0.10" };
    private readonly ComboBox _cmbSistema = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbTipo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    public Device Dispositivo { get; }

    public DeviceForm(Device original)
    {
        Dispositivo = new Device
        {
            Id = original.Id,
            Nome = original.Nome,
            EnderecoIp = original.EnderecoIp,
            SistemaOperacional = original.SistemaOperacional,
            Tipo = original.Tipo,
            DataCadastro = original.DataCadastro
        };

        Text = original.Id == 0 ? "Novo dispositivo" : "Editar dispositivo";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 215);

        _cmbSistema.DataSource = Enum.GetValues<SistemaOperacional>();
        _cmbTipo.DataSource = Enum.GetValues<TipoDispositivo>();

        _txtNome.Text = Dispositivo.Nome;
        _txtIp.Text = Dispositivo.EnderecoIp;
        _cmbSistema.SelectedItem = Dispositivo.SistemaOperacional;
        _cmbTipo.SelectedItem = Dispositivo.Tipo;

        var tabela = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 160,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 4
        };
        tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AdicionarLinha(tabela, 0, "Nome", _txtNome);
        AdicionarLinha(tabela, 1, "Endereço IP", _txtIp);
        AdicionarLinha(tabela, 2, "Sistema operacional", _cmbSistema);
        AdicionarLinha(tabela, 3, "Tipo", _cmbTipo);

        var btnSalvar = new Button { Text = "Salvar", Location = new Point(188, 172), Size = new Size(75, 28) };
        var btnCancelar = new Button
        {
            Text = "Cancelar",
            Location = new Point(270, 172),
            Size = new Size(75, 28),
            DialogResult = DialogResult.Cancel
        };
        btnSalvar.Click += (_, _) => Salvar();

        Controls.Add(tabela);
        Controls.Add(btnSalvar);
        Controls.Add(btnCancelar);
        AcceptButton = btnSalvar;
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
        Dispositivo.Nome = _txtNome.Text.Trim();
        Dispositivo.EnderecoIp = _txtIp.Text.Trim();
        Dispositivo.SistemaOperacional = (SistemaOperacional)_cmbSistema.SelectedItem!;
        Dispositivo.Tipo = (TipoDispositivo)_cmbTipo.SelectedItem!;

        // Validação de formato aqui para o usuário corrigir sem fechar o diálogo.
        // Unicidade do IP é verificada pelo serviço.
        var erros = DeviceValidator.Validar(Dispositivo);
        if (erros.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, erros), "Dados inválidos",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
