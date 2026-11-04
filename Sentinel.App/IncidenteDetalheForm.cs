using Sentinel.Core.Enums;
using Sentinel.Core.Services;

namespace Sentinel.App;

/// <summary>Detalhes de um incidente: eventos vinculados, histórico (trilha de auditoria) e ações.</summary>
public class IncidenteDetalheForm : Form
{
    private readonly IncidentService _servico;
    private readonly int _id;

    private readonly Label _lblTitulo = new() { Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI", 13f, FontStyle.Bold), Padding = new Padding(8, 6, 0, 0) };
    private readonly Label _lblInfo = new() { Dock = DockStyle.Top, Height = 26, Padding = new Padding(8, 5, 0, 0) };
    private readonly TextBox _txtDescricao = new() { Dock = DockStyle.Top, Height = 70, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
    private readonly DataGridView _gradeEventos = Grades.Criar();
    private readonly TextBox _txtHistorico = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
    private readonly TextBox _txtNota = new()
    {
        Dock = DockStyle.Top, Height = 56, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical,
        MaxLength = IncidentService.TamanhoMaximoJustificativa,
        PlaceholderText = "Escreva uma nota. Ela também serve de justificativa ao mudar o status ou o risco (obrigatória para resolver)."
    };
    private readonly FlowLayoutPanel _flowStatus = new() { AutoSize = true, WrapContents = true };
    private readonly ComboBox _cmbRisco = new() { DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true, Width = 100 };

    public IncidenteDetalheForm(IncidentService servico, int id)
    {
        _servico = servico;
        _id = id;

        MinimumSize = new Size(820, 560);
        Size = new Size(980, 680);
        StartPosition = FormStartPosition.CenterParent;

        _gradeEventos.Columns.AddRange(
            Grades.Coluna(nameof(LinhaEvento.DataHora), "Data/hora", 24, "dd/MM HH:mm:ss"),
            Grades.Coluna(nameof(LinhaEvento.Dispositivo), "Dispositivo", 24),
            Grades.Coluna(nameof(LinhaEvento.Nivel), "Gravidade", 16),
            Grades.Coluna(nameof(LinhaEvento.Descricao), "Descrição", 50));

        _cmbRisco.DataSource = Enum.GetValues<Gravidade>();
        _cmbRisco.Format += (_, e) => { if (e.ListItem is Gravidade g) e.Value = g.Rotulo(); };

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 200};
        split.Panel1.Controls.Add(_gradeEventos);
        split.Panel1.Controls.Add(Secao("Eventos vinculados"));
        split.Panel2.Controls.Add(_txtHistorico);
        split.Panel2.Controls.Add(Secao("Histórico (notas e mudanças, em ordem)"));

        var btnNota = new Button { Text = "Adicionar nota", AutoSize = true };
        btnNota.Click += (_, _) => AdicionarNota();
        var btnRisco = new Button { Text = "Alterar risco", AutoSize = true };
        btnRisco.Click += (_, _) => AlterarRisco();

        var acoes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 4, 0, 0) };
        acoes.Controls.Add(btnNota);
        acoes.Controls.Add(new Label { Text = "  Mover para:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        acoes.Controls.Add(_flowStatus);
        acoes.Controls.Add(new Label { Text = "  Risco:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        acoes.Controls.Add(_cmbRisco);
        acoes.Controls.Add(btnRisco);

        var rodape = new Panel { Dock = DockStyle.Bottom, Height = 130, Padding = new Padding(8, 4, 8, 4) };
        rodape.Controls.Add(acoes);
        rodape.Controls.Add(_txtNota);

        Controls.Add(split);
        Controls.Add(rodape);
        Controls.Add(_txtDescricao);
        Controls.Add(_lblInfo);
        Controls.Add(_lblTitulo);

        Recarregar();
    }

    private static Label Secao(string texto) => new()
    {
        Text = texto, Dock = DockStyle.Top, Height = 24, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        Padding = new Padding(4, 4, 0, 0)
    };

    private void Recarregar()
    {
        var incidente = Dialogos.TentarObter(() => _servico.Obter(_id), "Não consegui carregar o incidente.");
        if (incidente is null)
        {
            Close();
            return;
        }

        Text = $"Incidente #{incidente.Id} — {incidente.Titulo}";
        _lblTitulo.Text = $"#{incidente.Id}  {incidente.Titulo}";
        _lblInfo.Text = $"Status: {incidente.Status.Rotulo()}   |   Risco: {incidente.Risco.Rotulo()}   |   Aberto em {incidente.DataAbertura:dd/MM/yyyy HH:mm}"
            + (incidente.DataResolucao is { } resolvido ? $"   |   Resolvido em {resolvido:dd/MM/yyyy HH:mm}" : "");
        _lblInfo.BackColor = Cores.Fundo(incidente.Risco);
        _txtDescricao.Text = incidente.Descricao;

        _gradeEventos.DataSource = incidente.Eventos
            .Select(v => v.SecurityEvent)
            .OrderBy(e => e.DataHora)
            .Select(LinhaEvento.De)
            .ToList();

        _txtHistorico.Text = string.Join(Environment.NewLine + Environment.NewLine,
            incidente.Notas.Select(n => $"[{n.DataHora:dd/MM/yyyy HH:mm}] {n.Texto}"));
        _txtHistorico.SelectionStart = _txtHistorico.TextLength;
        _txtHistorico.ScrollToCaret();

        _cmbRisco.SelectedItem = incidente.Risco;
        ReconstruirBotoesDeStatus(incidente.Status);
    }

    private void ReconstruirBotoesDeStatus(StatusIncidente atual)
    {
        _flowStatus.Controls.Clear();
        foreach (var destino in IncidentService.TransicoesPermitidas(atual))
        {
            var alvo = destino;
            var botao = new Button { Text = TextoDoBotao(atual, alvo), AutoSize = true };
            botao.Click += (_, _) => MudarStatus(alvo);
            _flowStatus.Controls.Add(botao);
        }
    }

    private static string TextoDoBotao(StatusIncidente atual, StatusIncidente destino) => (atual, destino) switch
    {
        (StatusIncidente.Aberto, StatusIncidente.EmAnalise) => "Iniciar análise",
        (_, StatusIncidente.Resolvido) => "Resolver (exige nota)",
        (StatusIncidente.Resolvido, StatusIncidente.EmAnalise) => "Reabrir (em análise)",
        (StatusIncidente.EmAnalise, StatusIncidente.Aberto) => "Devolver para Aberto",
        _ => destino.Rotulo()
    };

    private void AdicionarNota()
    {
        if (!Dialogos.Tentar(() => _servico.AdicionarNota(_id, _txtNota.Text), "Não consegui salvar a nota. Nada foi alterado.")) return;
        _txtNota.Clear();
        Recarregar();
    }

    private void MudarStatus(StatusIncidente destino)
    {
        if (!Dialogos.Tentar(() => _servico.MudarStatus(_id, destino, _txtNota.Text), "Não consegui alterar o status. Nada foi alterado.")) return;
        _txtNota.Clear();
        Recarregar();
    }

    private void AlterarRisco()
    {
        var novo = (Gravidade)_cmbRisco.SelectedItem!;
        if (!Dialogos.Tentar(() => _servico.AlterarRisco(_id, novo, _txtNota.Text), "Não consegui alterar o risco. Nada foi alterado.")) return;
        _txtNota.Clear();
        Recarregar();
    }
}
