using Sentinel.Core.Enums;
using Sentinel.Core.Services;

namespace Sentinel.App;

/// <summary>Lista de incidentes com filtros. Duplo clique (ou Enter) abre os detalhes.</summary>
public class IncidentesForm : Form
{
    private readonly IncidentService _servico;
    private readonly ComboBox _cmbStatus = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly ComboBox _cmbRisco = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly TextBox _txtBusca = new() { PlaceholderText = "Buscar no título...", Width = 220 };
    private readonly Button _btnNovo = new() { Text = "Novo incidente", AutoSize = true };
    private readonly Button _btnDetalhes = new() { Text = "Abrir detalhes", AutoSize = true, Enabled = false };
    private readonly DataGridView _grade = Grades.Criar();
    private readonly Label _lblTotal = new() { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(8, 6, 0, 0) };

    public IncidentesForm(IncidentService servico)
    {
        _servico = servico;

        Text = "SENTINEL — Incidentes";
        MinimumSize = new Size(760, 420);
        Size = new Size(960, 520);
        StartPosition = FormStartPosition.CenterParent;

        _cmbStatus.Items.Add("Todos os status");
        foreach (var s in Enum.GetValues<StatusIncidente>()) _cmbStatus.Items.Add(s.Rotulo());
        _cmbRisco.Items.Add("Todos os riscos");
        foreach (var g in Enum.GetValues<Gravidade>()) _cmbRisco.Items.Add(g.Rotulo());
        _cmbStatus.SelectedIndex = 0;
        _cmbRisco.SelectedIndex = 0;

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        barra.Controls.AddRange(new Control[] { _txtBusca, _cmbStatus, _cmbRisco, _btnNovo, _btnDetalhes });

        _grade.Columns.AddRange(
            Grades.Coluna(nameof(LinhaIncidente.Id), "Nº", 5),
            Grades.Coluna(nameof(LinhaIncidente.Abertura), "Abertura", 14, "dd/MM/yyyy HH:mm"),
            Grades.Coluna(nameof(LinhaIncidente.Titulo), "Título", 40),
            Grades.Coluna(nameof(LinhaIncidente.Nivel), "Risco", 10),
            Grades.Coluna(nameof(LinhaIncidente.Situacao), "Status", 13),
            Grades.Coluna(nameof(LinhaIncidente.Eventos), "Eventos", 8));

        Controls.Add(_grade);
        Controls.Add(_lblTotal);
        Controls.Add(barra);

        _txtBusca.TextChanged += (_, _) => Recarregar();
        _cmbStatus.SelectedIndexChanged += (_, _) => Recarregar();
        _cmbRisco.SelectedIndexChanged += (_, _) => Recarregar();
        _btnNovo.Click += (_, _) => Novo();
        _btnDetalhes.Click += (_, _) => AbrirDetalhes();
        _grade.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) AbrirDetalhes(); };
        _grade.SelectionChanged += (_, _) => _btnDetalhes.Enabled = _grade.CurrentRow?.DataBoundItem is LinhaIncidente;
        _grade.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { AbrirDetalhes(); e.SuppressKeyPress = true; } };

        Recarregar();
    }

    private void Recarregar()
    {
        var idSelecionado = (_grade.CurrentRow?.DataBoundItem as LinhaIncidente)?.Id;

        StatusIncidente? status = _cmbStatus.SelectedIndex > 0 ? Enum.GetValues<StatusIncidente>()[_cmbStatus.SelectedIndex - 1] : null;
        Gravidade? risco = _cmbRisco.SelectedIndex > 0 ? Enum.GetValues<Gravidade>()[_cmbRisco.SelectedIndex - 1] : null;

        var lista = Dialogos.TentarObter(() => _servico.Listar(status, risco, _txtBusca.Text), "Não consegui carregar os incidentes.");
        if (lista is null) return;

        _grade.DataSource = lista.Select(LinhaIncidente.De).ToList();
        Grades.SelecionarPorId<LinhaIncidente>(_grade, idSelecionado, l => l.Id);

        var filtrando = status.HasValue || risco.HasValue || _txtBusca.Text.Length > 0;
        _lblTotal.Text = lista.Count switch
        {
            0 when filtrando => "Nenhum incidente encontrado com esses filtros.",
            0 => "Nenhum incidente ainda. Abra um a partir de um alerta, na tela de Monitoramento, ou crie um manualmente.",
            1 => "1 incidente",
            _ => $"{lista.Count} incidentes"
        };
    }

    private void Novo()
    {
        using var form = new IncidenteNovoForm();
        if (form.ShowDialog(this) != DialogResult.OK) return;

        var incidente = Dialogos.TentarObter(
            () => _servico.Abrir(form.Titulo, form.Descricao, form.Risco),
            "Não consegui abrir o incidente. Nada foi alterado.");
        if (incidente is null) return;

        Recarregar();
        Detalhar(incidente.Id);
    }

    private void AbrirDetalhes()
    {
        if (_grade.CurrentRow?.DataBoundItem is LinhaIncidente linha) Detalhar(linha.Id);
    }

    private void Detalhar(int id)
    {
        using (var detalhe = new IncidenteDetalheForm(_servico, id))
            detalhe.ShowDialog(this);
        Recarregar();
    }
}