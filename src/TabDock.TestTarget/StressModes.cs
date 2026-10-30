using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TabDock.TestTarget;

/// <summary>Pruebas de estrés: muchas ventanas en un solo proceso y ráfagas de abrir y cerrar.</summary>
internal static class StressModes
{
    private static readonly TimeSpan s_burstLifetime = TimeSpan.FromSeconds(1);

    public static void Add(Window main, TargetOptions options)
    {
        if (options.WindowCount > 1)
            main.ContentRendered += (_, _) => OpenExtraWindows(main, options);

        if (options.BurstCount > 0)
            main.ContentRendered += (_, _) => StartBurst(main, options);
    }

    /// <summary>Ventanas adicionales "título #2", "título #3"…, escalonadas para que no se tapen del todo.</summary>
    private static void OpenExtraWindows(Window main, TargetOptions options)
    {
        for (var n = 2; n <= options.WindowCount; n++)
            Create(main, options, string.Create(CultureInfo.InvariantCulture, $"{options.Title} #{n}"), offset: n).Show();
    }

    private static void StartBurst(Window main, TargetOptions options)
    {
        var opened = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(options.BurstIntervalMilliseconds) };
        timer.Tick += (_, _) =>
        {
            if (++opened > options.BurstCount)
            {
                timer.Stop();

                // Aviso para los scripts: la última ventana de la ráfaga se cierra al segundo.
                var done = new DispatcherTimer { Interval = s_burstLifetime + TimeSpan.FromMilliseconds(500) };
                done.Tick += (_, _) =>
                {
                    done.Stop();
                    main.Title += " (ráfaga terminada)";
                };
                done.Start();
                return;
            }

            var window = Create(main, options, string.Create(CultureInfo.InvariantCulture, $"{options.Title} ráfaga {opened}"), offset: opened % 20);
            window.Show();
            var close = new DispatcherTimer { Interval = s_burstLifetime };
            close.Tick += (_, _) =>
            {
                close.Stop();
                window.Close();
            };
            close.Start();
        };
        timer.Start();
    }

    private static Window Create(Window main, TargetOptions options, string title, int offset) => new()
    {
        Title = title,
        Width = options.Width,
        Height = options.Height,
        Left = main.Left + (offset * 12),
        Top = main.Top + (offset * 12),
        Content = new TextBlock { Text = title, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
    };
}
