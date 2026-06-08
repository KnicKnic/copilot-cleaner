using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace CopilotCleaner;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        IClassicDesktopStyleApplicationLifetime? desktop = null;
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            if (desktop is not null)
            {
                Dispatcher.UIThread.Post(() => desktop.Shutdown());
            }
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, lifetime => desktop = lifetime);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
