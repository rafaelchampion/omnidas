using System.Windows;

namespace Omnidas.Gui;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        string? initialPath = e.Args.Length > 0 ? e.Args[0] : null;
        var mainWindow = new MainWindow(initialPath);
        mainWindow.Show();
    }
}
