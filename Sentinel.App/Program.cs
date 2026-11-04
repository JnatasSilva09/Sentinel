using Sentinel.Core.Deteccao;
using Sentinel.Core.Services;
using Sentinel.Core.Simulacao;
using Sentinel.Data;

namespace Sentinel.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            // O banco fica em %LocalAppData%\Sentinel, fora da pasta do projeto.
            var pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel");
            Directory.CreateDirectory(pasta);

            var opcoes = SentinelDbOptions.Criar($"Data Source={Path.Combine(pasta, "sentinel.db")}");
            DatabaseInitializer.Inicializar(opcoes);

            var repositorioDispositivos = new DeviceRepository(opcoes);
            var repositorioMonitoramento = new MonitoramentoRepository(opcoes);
            var motor = MotorDeDeteccao.Padrao();

            var dispositivos = new DeviceService(repositorioDispositivos);
            var monitoramento = new MonitoramentoService(
                repositorioMonitoramento, repositorioDispositivos, new SimuladorDeEventos(), motor);
            var incidentes = new IncidentService(new IncidentRepository(opcoes), repositorioMonitoramento, motor);
            var dashboard = new DashboardService(new DashboardRepository(opcoes));

            Application.Run(new MainForm(dispositivos, monitoramento, incidentes, dashboard));
        }
        catch (Exception ex)
        {
            ErrorLog.Registrar(ex);
            MessageBox.Show(
                "Não consegui abrir o SENTINEL." + Environment.NewLine + Environment.NewLine +
                "Se for a primeira vez, confira se a migration inicial foi criada (veja o README). " +
                "Os detalhes técnicos foram salvos em:" + Environment.NewLine + ErrorLog.CaminhoDoArquivo,
                "SENTINEL", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
