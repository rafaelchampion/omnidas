using Microsoft.UI.Xaml;
using System;

namespace Omnidas_WinUI;

public partial class App : Application
{
    public static string? TargetPath { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Length > 1)
        {
            TargetPath = cmdArgs[1];
        }

        m_window = new MainWindow(TargetPath);
        m_window.Activate();
    }

    private Window? m_window;
}
