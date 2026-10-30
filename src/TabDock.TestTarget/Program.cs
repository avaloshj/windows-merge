using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TabDock.TestTarget;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = TargetOptions.Parse(args);
        if (options.DpiUnaware)
            TestModes.MakeThreadDpiUnaware();

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };

        var window = new Window
        {
            Title = options.Title,
            Width = options.Width,
            Height = options.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = options.ToolWindow ? WindowStyle.ToolWindow : WindowStyle.SingleBorderWindow,
            Content = new TextBlock
            {
                Text = options.Title,
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.DimGray,
            },
        };

        if (options.DestroyEventOnHide)
            TestModes.RaiseDestroyEventOnHide(window);

        if (options.CustomChrome || options.Frameless)
            TestModes.ApplyCustomChrome(window, keepResizeEdges: options.CustomChrome);

        if (options.ThumbnailOf is { } source)
        {
            window.Background = Brushes.Magenta;
            window.Topmost = true;
            ThumbnailHost.Attach(window, source);
        }

        if (options.Maximized)
            window.WindowState = WindowState.Maximized;

        AddTimedBehaviors(window, options);
        StressModes.Add(window, options);

        if (options.OwnerDialog)
        {
            window.ContentRendered += (_, _) => new Window
            {
                Title = options.Title + " - diálogo",
                Owner = window,
                Width = 360,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }.Show();
        }

        return app.Run(window);
    }

    /// <summary>Comportamientos que empiezan tras el primer render: cambio de título y bloqueo del hilo de UI.</summary>
    private static void AddTimedBehaviors(Window window, TargetOptions options)
    {
        if (options.HangAfterMilliseconds is { } hang)
        {
            var hangTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(hang) };
            hangTimer.Tick += (_, _) =>
            {
                hangTimer.Stop();
                Thread.Sleep(Timeout.Infinite);   // deja de procesar mensajes: Windows la marcará como "no responde"
            };
            window.ContentRendered += (_, _) => hangTimer.Start();
        }

        if (options.RetitleAfterMilliseconds is { } delay)
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.Title = options.Title + " (renombrada)";
            };
            window.ContentRendered += (_, _) => timer.Start();
        }
    }
}
