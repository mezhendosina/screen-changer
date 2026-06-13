using Wpf.Ui.Appearance;
using WpfApp = System.Windows.Application;

namespace ScreenChanger;

public partial class App
{
    private static Mutex? _mutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _mutex = new Mutex(true, "ScreenChangerSingleInstance", out bool isNew);
        if (!isNew)
        {
            _mutex.Dispose();
            System.Windows.MessageBox.Show("Screen Changer уже запущен.", "Screen Changer",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
