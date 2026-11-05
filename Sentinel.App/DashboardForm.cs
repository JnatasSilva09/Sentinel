using Sentinel.Core.Dashboard;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;

namespace Sentinel.App;

/// <summary>Visão geral da segurança: números-chave e gráficos. Pode ficar aberto ao lado do Monitoramento.</summary>
public class DashboardForm : Form
{
    private readonly DashboardService _servico;
    private readonly ComboBox _cmbPeriodo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly CheckBox _chkAuto = new() { Text = "Atualizar automaticamente (3 s)", AutoSize = true, Checked = true, Padding = new Padding(8, 4, 0, 0) };
    private readonly Label _lblJanela = new() { AutoSize = true, Padding = new Padding(8, 6, 0, 0), ForeColor = Color.FromArgb(90, 90, 90) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };

    private readonly Label _kpiEventos, _kpiAlertas, _kpiIncidentes, _kpiDispositivos;
    private readonly Label _detEventos, _detAlertas, _detIncidentes, _detDispositivos;

    private readonly GraficoBarras _gGravidade = new() { Titulo = "Eventos por gravidade", Dock = DockStyle.Fill, Margin = new Padding(4) };
    private readonly GraficoBarras _gTipo = new() { Titulo = "Eventos por tipo", Dock = DockStyle.Fill, Margin = new Padding(4) };
    private readonly GraficoBarras _gTempo = new() { Titulo = "Eventos ao longo do tempo", Modo = GraficoBarras.ModoGrafico.Vertical, Dock = DockStyle.Fill, Margin = new Padding(4) };
    private readonly GraficoBarras _gTop = new() { Titulo = "Dispositivos com mais eventos", Dock = DockStyle.Fill, Margin = new Padding(4) };
    private readonly GraficoBarras _gIncidentes = new() { Titulo = "Incidentes por status (todos)", Dock = DockStyle.Fill, Margin = new Padding(4) };

    public DashboardForm(DashboardService servico)
    {
        _servico = servico;

        Text = "SENTINEL — Dashboard de segurança (dados simulados)";
        MinimumSize = new Size(820, 640);
        Size = new Size(1000, 720);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(240, 240, 240);

        var cartoes = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, ColumnCount = 4, RowCount = 1, Padding = new Padding(4) };
        for (var i = 0; i < 4; i++) cartoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cartoes.Controls.Add(Cartao("Eventos no período", out _kpiEventos, out _detEventos), 0, 0);
        cartoes.Controls.Add(Cartao("Alertas no período", out _kpiAlertas, out _detAlertas), 1, 0);
        cartoes.Controls.Add(Cartao("Incidentes em andamento", out _kpiIncidentes, out _detIncidentes), 2, 0);
        cartoes.Controls.Add(Cartao("Dispositivos monitorados", out _kpiDispositivos, out _detDispositivos), 3, 0);

        var graficos = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(4) };
        graficos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        graficos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 3; i++) graficos.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        graficos.Controls.Add(_gGravidade, 0, 0);
        graficos.Controls.Add(_gTipo, 1, 0);
        graficos.Controls.Add(_gTempo, 0, 1);
        graficos.SetColumnSpan(_gTempo, 2);
        graficos.Controls.Add(_gTop, 0, 2);
        graficos.Controls.Add(_gIncidentes, 1, 2);

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        barra.Controls.Add(new Label { Text = "Período:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        foreach (var p in Enum.GetValues<PeriodoDashboard>()) _cmbPeriodo.Items.Add(p.Rotulo());
        _cmbPeriodo.SelectedIndex = (int)PeriodoDashboard.Ultimas24Horas;
        barra.Controls.Add(_cmbPeriodo);
        var btnAtualizar = new Button { Text = "Atualizar agora", AutoSize = true };
        btnAtualizar.Click += (_, _) => Atualizar();
        barra.Controls.Add(btnAtualizar);
        barra.Controls.Add(_chkAuto);
        barra.Controls.Add(_lblJanela);

        Controls.Add(graficos);
        Controls.Add(cartoes);
        Controls.Add(barra);

        _cmbPeriodo.SelectedIndexChanged += (_, _) => Atualizar();
        _chkAuto.CheckedChanged += (_, _) => _timer.Enabled = _chkAuto.Checked;
        _timer.Tick += (_, _) => Atualizar();
        FormClosed += (_, _) => { _timer.Stop(); _timer.Dispose(); };

        Atualizar();
        _timer.Enabled = _chkAuto.Checked;
    }

    private static Panel Cartao(string titulo, out Label valor, out Label detalhe)
    {
        var painel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(4), Padding = new Padding(10, 6, 10, 6) };
        var lblTitulo = new Label { Text = titulo, Dock = DockStyle.Top, Height = 20, ForeColor = Color.FromArgb(90, 90, 90) };
        detalhe = new Label { Dock = DockStyle.Bottom, Height = 18, ForeColor = Color.FromArgb(110, 110, 110) };
        valor = new Label
        {
            Dock = DockStyle.Fill, Text = "0", TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 22f, FontStyle.Bold)
        };
        painel.Controls.Add(valor);
        painel.Controls.Add(detalhe);
        painel.Controls.Add(lblTitulo);
        return painel;
    }

    private void Atualizar()
    {
        var periodo = (PeriodoDashboard)_cmbPeriodo.SelectedIndex;
        var resumo = Dialogos.TentarObter(() => _servico.Obter(periodo), "Não consegui carregar o dashboard.");
        if (resumo is null)
        {
            _chkAuto.Checked = false; // evita repetir o erro a cada 3 s
            return;
        }

        _lblJanela.Text = $"Janela: {resumo.Desde:dd/MM HH:mm} até {resumo.Ate:dd/MM HH:mm}";

        _kpiEventos.Text = resumo.TotalEventos.ToString();
        _detEventos.Text = periodo.Rotulo();
        _kpiAlertas.Text = resumo.TotalAlertas.ToString();
        _detAlertas.Text = $"{resumo.AlertasPorGravidade[Gravidade.Critica]} críticos · {resumo.AlertasPorGravidade[Gravidade.Alta]} altos";
        _kpiIncidentes.Text = resumo.IncidentesEmAndamento.ToString();
        _detIncidentes.Text = $"{resumo.IncidentesPorStatus[StatusIncidente.Aberto]} abertos · {resumo.IncidentesPorStatus[StatusIncidente.EmAnalise]} em análise";
        _kpiDispositivos.Text = resumo.TotalDispositivos.ToString();
        _detDispositivos.Text = "cadastrados";

        _gGravidade.Definir(Enum.GetValues<Gravidade>()
            .Select(g => new GraficoBarras.Item(g.Rotulo(), resumo.EventosPorGravidade[g], Cores.Forte(g))));
        _gTipo.Definir(Enum.GetValues<TipoEvento>()
            .Select(t => new GraficoBarras.Item(t.Rotulo(), resumo.EventosPorTipo[t], Cores.Neutra)));
        _gTempo.Definir(resumo.EventosAoLongoDoTempo
            .Select(p => new GraficoBarras.Item(p.Inicio.ToString(periodo.FormatoDoRotulo()), p.Total, Cores.Neutra)));
        _gTop.Definir(resumo.DispositivosMaisAtivos
            .Select(d => new GraficoBarras.Item(d.Nome, d.Total, Cores.Neutra)));
        _gIncidentes.Definir(Enum.GetValues<StatusIncidente>()
            .Select(s => new GraficoBarras.Item(s.Rotulo(), resumo.IncidentesPorStatus[s], Cores.Status(s))));
    }
}
