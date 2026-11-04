using Sentinel.Core.Entities;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;

namespace Sentinel.App;

public class MainForm : Form
{
    private const string TodosOsSistemas = "Todos os sistemas";

    private readonly DeviceService _servico;
    private readonly MonitoramentoService _monitoramento;
    private readonly IncidentService _incidentes;
    private readonly DashboardService _dashboard;
    private readonly Button _btnMonitor = new() { Text = "Monitoramento", AutoSize = true };
    private readonly Button _btnIncidentes = new() { Text = "Incidentes", AutoSize = true };
    private readonly Button _btnDashboard = new() { Text = "Dashboard", AutoSize = true };
    private MonitorForm? _janelaMonitor;
    private DashboardForm? _janelaDashboard;
    private readonly TextBox _txtBusca = new() { PlaceholderText = "Buscar por nome ou IP...", Width = 240 };
    private readonly ComboBox _cmbSistema = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly DataGridView _grade = new();
    private readonly Button _btnNovo = new() { Text = "Novo", AutoSize = true };
    private readonly Button _btnEditar = new() { Text = "Editar", AutoSize = true };
    private readonly Button _btnExcluir = new() { Text = "Excluir", AutoSize = true };
    private readonly Label _lblTotal = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private readonly Label _lblAviso = new()
    {
        Text = "Ambiente de demonstração: todos os dispositivos e eventos são simulados. Nada aqui toca uma rede real.",
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 26,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 0, 0),
        BackColor = Color.FromArgb(255, 244, 214),
        ForeColor = Color.FromArgb(90, 62, 0)
    };
    private readonly ToolTip _dicas = new();

    public MainForm(
        DeviceService servico,
        MonitoramentoService monitoramento,
        IncidentService incidentes,
        DashboardService dashboard)
    {
        _servico = servico;
        _monitoramento = monitoramento;
        _incidentes = incidentes;
        _dashboard = dashboard;

        Text = "SENTINEL — Dispositivos (dados simulados)";
        MinimumSize = new Size(760, 420);
        Size = new Size(1000, 520);
        StartPosition = FormStartPosition.CenterScreen;

        MontarLayout();

        _cmbSistema.Items.Add(TodosOsSistemas);
        _cmbSistema.Items.AddRange(Enum.GetNames<SistemaOperacional>());
        _cmbSistema.SelectedIndex = 0;

        _txtBusca.TextChanged += (_, _) => Recarregar();
        _cmbSistema.SelectedIndexChanged += (_, _) => Recarregar();
        _btnNovo.Click += (_, _) => Novo();
        _btnEditar.Click += (_, _) => Editar();
        _btnExcluir.Click += (_, _) => Excluir();
        // Monitoramento e Dashboard abrem em janelas livres: dá para simular eventos e ver os gráficos mudando.
        _btnMonitor.Click += (_, _) => AbrirUnica(ref _janelaMonitor, () => new MonitorForm(_monitoramento, _incidentes));
        _btnDashboard.Click += (_, _) => AbrirUnica(ref _janelaDashboard, () => new DashboardForm(_dashboard));
        _btnIncidentes.Click += (_, _) => { using var lista = new IncidentesForm(_incidentes); lista.ShowDialog(this); };
        _grade.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Editar(); };
        _grade.SelectionChanged += (_, _) => AtualizarBotoes();

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.N) { Novo(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Delete && _grade.Focused) { Excluir(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter && _grade.Focused) { Editar(); e.SuppressKeyPress = true; }
        };

        Recarregar();
    }

    private void MontarLayout()
    {
        var barra = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true
        };
        barra.Controls.AddRange(new Control[] { _txtBusca, _cmbSistema, _btnNovo, _btnEditar, _btnExcluir, _btnMonitor, _btnIncidentes, _btnDashboard });

        _grade.Dock = DockStyle.Fill;
        _grade.BackgroundColor = SystemColors.Window;
        _grade.ReadOnly = true;
        _grade.AllowUserToAddRows = false;
        _grade.AllowUserToDeleteRows = false;
        _grade.MultiSelect = false;
        _grade.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grade.RowHeadersVisible = false;
        _grade.AutoGenerateColumns = false;
        _grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grade.Columns.AddRange(
            Coluna(nameof(Device.Nome), "Nome", 30),
            Coluna(nameof(Device.EnderecoIp), "Endereço IP", 18),
            Coluna(nameof(Device.SistemaOperacional), "Sistema operacional", 18),
            Coluna(nameof(Device.Tipo), "Tipo", 14),
            Coluna(nameof(Device.DataCadastro), "Cadastro", 20, "dd/MM/yyyy HH:mm"));

        var rodape = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(8, 0, 8, 0) };
        rodape.Controls.Add(_lblTotal);

        _dicas.SetToolTip(_btnNovo, "Cadastrar um novo dispositivo (Ctrl+N)");
        _dicas.SetToolTip(_btnEditar, "Editar o dispositivo selecionado (Enter ou duplo clique)");
        _dicas.SetToolTip(_btnExcluir, "Excluir o dispositivo selecionado (Delete)");

        Controls.Add(_grade);
        Controls.Add(rodape);
        Controls.Add(barra);
        Controls.Add(_lblAviso);
    }

    private static DataGridViewTextBoxColumn Coluna(string propriedade, string titulo, float peso, string? formato = null)
    {
        var coluna = new DataGridViewTextBoxColumn
        {
            DataPropertyName = propriedade,
            HeaderText = titulo,
            FillWeight = peso
        };
        if (formato is not null) coluna.DefaultCellStyle.Format = formato;
        return coluna;
    }

    /// <summary>Abre a janela se ainda não existe; se já está aberta, só traz para a frente.</summary>
    private void AbrirUnica<T>(ref T? janela, Func<T> criar) where T : Form
    {
        if (janela is null || janela.IsDisposed)
        {
            janela = criar();
            janela.Show(this);
            return;
        }

        if (janela.WindowState == FormWindowState.Minimized) janela.WindowState = FormWindowState.Normal;
        janela.Activate();
    }

    private Device? Selecionado => _grade.CurrentRow?.DataBoundItem as Device;

    private void Recarregar()
    {
        var idSelecionado = Selecionado?.Id;

        SistemaOperacional? filtro = _cmbSistema.SelectedIndex > 0
            ? Enum.Parse<SistemaOperacional>((string)_cmbSistema.SelectedItem!)
            : null;

        try
        {
            var lista = _servico.Listar(_txtBusca.Text, filtro);
            _grade.DataSource = lista.ToList();
            _lblTotal.Text = lista.Count switch
            {
                0 when _txtBusca.Text.Length > 0 || filtro.HasValue =>
                    "Nenhum dispositivo encontrado com esses filtros. Tente limpar a busca.",
                0 => "Nenhum dispositivo cadastrado ainda. Clique em \"Novo\" para começar.",
                1 => "1 dispositivo",
                _ => $"{lista.Count} dispositivos"
            };

            if (idSelecionado.HasValue)
            {
                foreach (DataGridViewRow linha in _grade.Rows)
                {
                    if (linha.DataBoundItem is Device d && d.Id == idSelecionado.Value)
                    {
                        linha.Selected = true;
                        _grade.CurrentCell = linha.Cells[0];
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MostrarErro("Não consegui carregar a lista de dispositivos.", ex);
        }

        AtualizarBotoes();
    }

    private void AtualizarBotoes()
    {
        var temSelecao = Selecionado is not null;
        _btnEditar.Enabled = temSelecao;
        _btnExcluir.Enabled = temSelecao;
    }

    private void Novo()
    {
        using var form = new DeviceForm(new Device());
        if (form.ShowDialog(this) != DialogResult.OK) return;

        if (Executar(() => _servico.Cadastrar(form.Dispositivo)))
            Recarregar();
    }

    private void Editar()
    {
        var atual = Selecionado;
        if (atual is null) return;

        using var form = new DeviceForm(atual);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        if (Executar(() => _servico.Atualizar(form.Dispositivo)))
            Recarregar();
    }

    private void Excluir()
    {
        var atual = Selecionado;
        if (atual is null) return;

        var resposta = MessageBox.Show(
            $"Tem certeza de que quer excluir \"{atual.Nome}\" ({atual.EnderecoIp})?{Environment.NewLine}{Environment.NewLine}" +
            "Os eventos de segurança ligados a ele também serão apagados, e isso não pode ser desfeito.",
            "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (resposta != DialogResult.Yes) return;

        if (Executar(() => _servico.Remover(atual.Id)))
            Recarregar();
    }

    /// <summary>Executa a ação mostrando erros de regra de negócio ao usuário. Retorna true se deu certo.</summary>
    private bool Executar(Action acao)
    {
        try
        {
            acao();
            return true;
        }
        catch (RegraDeNegocioException ex)
        {
            MessageBox.Show(ex.Message, "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MostrarErro("Não consegui concluir a operação. Nada foi alterado.", ex);
        }
        return false;
    }

    /// <summary>Mostra uma mensagem clara; os detalhes técnicos vão para o arquivo de log.</summary>
    private static void MostrarErro(string mensagem, Exception ex)
    {
        ErrorLog.Registrar(ex);
        MessageBox.Show(
            $"{mensagem}{Environment.NewLine}{Environment.NewLine}" +
            $"Os detalhes técnicos foram salvos em:{Environment.NewLine}{ErrorLog.CaminhoDoArquivo}",
            "Algo deu errado", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}