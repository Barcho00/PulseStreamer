using System.Windows;

namespace HeartRateMonitor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppLogger.Info("App", "PulseStreamer application started");
            LiveHeartRateStreamer.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LiveHeartRateStreamer.Shutdown();
            AppLogger.Info("App", "PulseStreamer application closed");
            base.OnExit(e);
        }
    }
}
