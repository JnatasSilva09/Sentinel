using Sentinel.Core.Assistente;
using Sentinel.Core.Enums;
using Sentinel.Core.Services;

namespace Sentinel.App;

/// <summary>Painel de monitoramento: gera eventos simulados e mostra eventos e alertas quase em tempo real.</summary>
public class MonitorForm : Form
{
    private readonly MonitoramentoService _servico;
    private readonly IncidentService _incidentes;
    private readonly AssistenteService _assistente;
    private readonly ExecutorIa _ia;
    private readonly DataGridView _gradeEventos = Grades.Criar();
    private readonly DataGridView _gradeAlertas = Grades.Criar();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    private readonly CheckBox _chkAuto = new() { Text = "Automático (a cada 2 s)", AutoSize = true, Padding = new Padding(8, 4, 0, 0) };
    private readonly Button _btnIncidente = new() { Text = "Abrir incidente do alerta selecionado", AutoSize = true, Enabled = false };
    private readonly Button _btnExplicar = new() { Text = "Explicar alerta (IA)", AutoSize = true, Enabled = false };
    private readonly Button _btnClassificar = new() { Text = "Sugerir gravidade do evento (IA)", AutoSize = true, Enabled = false };
    private readonly Label _lblStatus = new() { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(8, 6, 0, 0) };
    private readonly ToolTip _dicas = new();

    internal MonitorForm(MonitoramentoService servico, IncidentService incidentes, AssistenteService assistente)
    {
        _servico = servico;
        _incidentes = incidentes;
        _assistente = assistente;
        _ia = new ExecutorIa(this);

        Text = "SENTINEL — Monitoramento (dados simulados)";
        MinimumSize = new Size(900, 520);
        Size = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterParent;

        MontarLayout();

        _timer.Tick += (_, _) => ExecutarUm(() => _servico.SimularEvento());
        _chkAuto.CheckedChanged += (_, _) => _timer.Enabled = _chkAuto.Checked;
        _gradeAlertas.SelectionChanged += (_, _) =>
        {
            var temAlerta = _gradeAlertas.CurrentRow?.DataBoundItem is LinhaAlerta;
            _btnIncidente.Enabled = temAlerta;
            _btnExplicar.Enabled = temAlerta;
        };
        _gradeEventos.SelectionChanged += (_, _) => _btnClassificar.Enabled = _gradeEventos.CurrentRow?.DataBoundItem is LinhaEvento;
        _btnExplicar.Click += async (_, _) => await ExplicarAlerta();
        _btnClassificar.Click += async (_, _) => await SugerirGravidade();
        _btnIncidente.Click += (_, _) => AbrirIncidente();
        FormClosed += (_, _) => { _timer.Stop(); _timer.Dispose(); };

        Recarregar();
    }

    private void MontarLayout()
    {
        var aviso = new Label
        {
            Text = "Tudo aqui é simulado: nenhum evento vem de uma rede real e nenhuma varredura ou ataque é executado.",
            Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            BackColor = Color.FromArgb(255, 244, 214), ForeColor = Color.FromArgb(90, 62, 0)
        };

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        barra.Controls.Add(Botao("Evento aleatório", "Gera um evento qualquer em um dispositivo qualquer",
            () => ExecutarUm(() => _servico.SimularEvento())));
        barra.Controls.Add(Botao("Simular força bruta", "6 logins falhos em 50 segundos no mesmo dispositivo",
            () => ExecutarVarios(() => _servico.SimularRajada(TipoEvento.LoginFalho, 6, 10))));
        barra.Controls.Add(Botao("Simular varredura", "3 varreduras de portas em 1 minuto no mesmo dispositivo",
            () => ExecutarVarios(() => _servico.SimularRajada(TipoEvento.VarreduraDePortas, 3, 30))));
        barra.Controls.Add(Botao("Simular malware", "1 detecção de malware em um dispositivo",
            () => ExecutarVarios(() => _servico.SimularRajada(TipoEvento.MalwareDetectado, 1, 0))));
        barra.Controls.Add(_chkAuto);
        barra.Controls.Add(Botao("Limpar dados", "Apaga eventos e alertas simulados (incidentes são mantidos)", ConfirmarLimpeza));

        _dicas.SetToolTip(_btnExplicar, "O assistente explica o alerta e sugere medidas preventivas");
        _dicas.SetToolTip(_btnClassificar, "O assistente sugere uma gravidade; você decide se aplica");
        _dicas.SetToolTip(_btnIncidente, "Cria um incidente com os eventos que dispararam o alerta");

        _gradeEventos.Columns.AddRange(
            Grades.Coluna(nameof(LinhaEvento.DataHora), "Data/hora", 14, "dd/MM HH:mm:ss"),
            Grades.Coluna(nameof(LinhaEvento.Dispositivo), "Dispositivo", 16),
            Grades.Coluna(nameof(LinhaEvento.Tipo), "Tipo", 16),
            Grades.Coluna(nameof(LinhaEvento.Nivel), "Gravidade", 9),
            Grades.Coluna(nameof(LinhaEvento.Descricao), "Descrição", 45));

        _gradeAlertas.Columns.AddRange(
            Grades.Coluna(nameof(LinhaAlerta.DataHora), "Data/hora", 14, "dd/MM HH:mm:ss"),
            Grades.Coluna(nameof(LinhaAlerta.Dispositivo), "Dispositivo", 16),
            Grades.Coluna(nameof(LinhaAlerta.Regra), "Regra", 18),
            Grades.Coluna(nameof(LinhaAlerta.Nivel), "Gravidade", 9),
            Grades.Coluna(nameof(LinhaAlerta.Descricao), "O que fazer", 43));

        // Linhas do alerta podem ter texto longo: quebra de linha e altura automática.
        _gradeAlertas.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _gradeAlertas.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

        var barraAlertas = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 0, 4, 2) };
        barraAlertas.Controls.Add(_btnIncidente);
        barraAlertas.Controls.Add(_btnExplicar);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };
        var barraEventos = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 0, 4, 2) };
        barraEventos.Controls.Add(_btnClassificar);

        split.Panel1.Controls.Add(_gradeEventos);
        split.Panel1.Controls.Add(barraEventos);
        split.Panel1.Controls.Add(Titulo("Eventos de segurança (mais recentes primeiro)"));
        split.Panel2.Controls.Add(_gradeAlertas);
        split.Panel2.Controls.Add(barraAlertas);
        split.Panel2.Controls.Add(Titulo("Alertas gerados pelas regras de detecção"));

        Controls.Add(split);
        Controls.Add(_lblStatus);
        Controls.Add(barra);
        Controls.Add(aviso);
    }

    private Button Botao(string texto, string dica, Action acao)
    {
        var botao = new Button { Text = texto, AutoSize = true };
        botao.Click += (_, _) => acao();
        _dicas.SetToolTip(botao, dica);
        return botao;
    }

    private static Label Titulo(string texto) => new()
    {
        Text = texto, Dock = DockStyle.Top, Height = 24, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        Padding = new Padding(4, 4, 0, 0)
    };

    private void Recarregar()
    {
        var alertaSelecionado = (_gradeAlertas.CurrentRow?.DataBoundItem as LinhaAlerta)?.Id;

        _gradeEventos.DataSource = _servico.UltimosEventos(200).Select(LinhaEvento.De).ToList();
        _gradeAlertas.DataSource = _servico.UltimosAlertas(100).Select(LinhaAlerta.De).ToList();

        // O modo automático recarrega a cada 2 s; sem isso a seleção do alerta voltaria para a primeira linha.
        Grades.SelecionarPorId<LinhaAlerta>(_gradeAlertas, alertaSelecionado, l => l.Id);
    }

    private void ExecutarUm(Func<ResultadoRegistro> acao) => ExecutarVarios(() => new[] { acao() });

    private void ExecutarVarios(Func<IEnumerable<ResultadoRegistro>> acao)
    {
        try
        {
            var resultados = acao().ToList();
            Recarregar();

            var alertas = resultados.SelectMany(r => r.Alertas).ToList();
            _lblStatus.Text = alertas.Count == 0
                ? $"{resultados.Count} evento(s) registrado(s). Nenhuma regra foi disparada."
                : $"Atenção: {alertas.Count} novo(s) alerta(s) — {alertas[0].Regra}. Selecione-o e abra um incidente.";
        }
        catch (RegraDeNegocioException ex)
        {
            _chkAuto.Checked = false; // evita repetir o mesmo aviso a cada 2 s
            Dialogos.Aviso(ex.Message);
        }
        catch (Exception ex)
        {
            _chkAuto.Checked = false;
            Dialogos.Erro("Não consegui registrar o evento. Nada foi alterado.", ex);
        }
    }

    private void AbrirIncidente()
    {
        if (_gradeAlertas.CurrentRow?.DataBoundItem is not LinhaAlerta alerta) return;

        var incidente = Dialogos.TentarObter(
            () => _incidentes.AbrirAPartirDoAlerta(alerta.Id),
            "Não consegui abrir o incidente. Nada foi alterado.");
        if (incidente is null) return;

        using var detalhe = new IncidenteDetalheForm(_incidentes, _assistente, incidente.Id);
        detalhe.ShowDialog(this);
    }

    private async Task ExplicarAlerta()
    {
        if (_gradeAlertas.CurrentRow?.DataBoundItem is not LinhaAlerta alerta) return;

        await _ia.ExecutarAsync(
            ct => _assistente.ExplicarAlertaAsync(alerta.Id, ct),
            analise =>
            {
                var texto = analise.Explicacao
                    + Environment.NewLine + Environment.NewLine + "Medidas preventivas:" + Environment.NewLine
                    + string.Join(Environment.NewLine, analise.Medidas.Select((m, i) => $"{i + 1}. {m}"))
                    + Environment.NewLine + Environment.NewLine + $"Risco sugerido: {analise.RiscoSugerido.Rotulo()}";
                using var janela = new ResultadoIaForm($"Explicação do alerta — {alerta.Regra}", analise.Fonte, texto);
                janela.ShowDialog(this);
            },
            _btnExplicar, _btnClassificar);
    }

    private async Task SugerirGravidade()
    {
        if (_gradeEventos.CurrentRow?.DataBoundItem is not LinhaEvento evento) return;

        await _ia.ExecutarAsync(
            ct => _assistente.SugerirGravidadeAsync(evento.Id, ct),
            sugestao =>
            {
                var texto = $"Gravidade atual: {evento.Gravidade.Rotulo()}" + Environment.NewLine +
                            $"Gravidade sugerida: {sugestao.Gravidade.Rotulo()}" + Environment.NewLine + Environment.NewLine +
                            sugestao.Justificativa;

                var podeAplicar = sugestao.Gravidade != evento.Gravidade;
                using var janela = new ResultadoIaForm(
                    "Sugestão de gravidade", sugestao.Fonte, texto,
                    podeAplicar ? $"Aplicar \"{sugestao.Gravidade.Rotulo()}\"" : null,
                    podeAplicar ? () => AplicarGravidade(evento.Id, sugestao.Gravidade) : null);
                janela.ShowDialog(this);
            },
            _btnExplicar, _btnClassificar);
    }

    // A IA só sugere: a mudança só acontece aqui, depois do clique de uma pessoa.
    private bool AplicarGravidade(int eventoId, Gravidade nova)
    {
        var ok = Dialogos.Tentar(() => _servico.ReclassificarEvento(eventoId, nova), "Não consegui alterar a gravidade. Nada foi alterado.");
        if (ok)
        {
            Recarregar();
            _lblStatus.Text = $"Gravidade do evento alterada para {nova.Rotulo()} (sugestão aceita por você).";
        }
        return ok;
    }

    private void ConfirmarLimpeza()
    {
        var resposta = MessageBox.Show(
            "Apagar todos os eventos e alertas simulados?" + Environment.NewLine + Environment.NewLine +
            "Os dispositivos e os incidentes serão mantidos, mas os incidentes perdem o vínculo com os eventos apagados.",
            "Confirmar limpeza", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (resposta != DialogResult.Yes) return;

        if (Dialogos.Tentar(() => _servico.LimparDadosSimulados(), "Não consegui limpar os dados. Nada foi alterado."))
        {
            Recarregar();
            _lblStatus.Text = "Dados simulados apagados.";
        }
    }
}
