using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using TabDock.App.Converters;
using TabDock.App.ViewModels;
using TabDock.Core.Abstractions;
using TabDock.Win32.Dwm;

namespace TabDock.App.Views;

/// <summary>
/// Vista previa al pasar el ratón por una pestaña (400 ms): imagen en vivo compuesta por DWM si la ventana tiene
/// superficie visible y, si no (las pestañas inactivas están ocultas), una tarjeta con nombre, archivo y color.
/// No se activa ni roba el foco. La imagen nunca se guarda: la compone DWM directamente.
/// </summary>
internal sealed class TabPreviewWindow : Window
{
    private const double CardWidth = 300;
    private const double ThumbnailHeight = 170;

    private static readonly HexToBrushConverter s_brushes = new();

    private readonly Border _thumbnailArea;
    private readonly Ellipse _colorDot;
    private readonly TextBlock _name;
    private readonly TextBlock _subtitle;
    private LiveThumbnail? _thumbnail;

    public TabPreviewWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        SetResourceReference(BackgroundProperty, "Brush.BgWindow");
        SetResourceReference(BorderBrushProperty, "Brush.Border");
        BorderThickness = new Thickness(1);
        FontFamily = (FontFamily)FindResource("Font.Ui");

        _thumbnailArea = new Border { Width = CardWidth, Height = ThumbnailHeight, CornerRadius = new CornerRadius(6) };
        _thumbnailArea.SetResourceReference(Border.BackgroundProperty, "Brush.BgSidebar");
        _colorDot = new Ellipse { Width = 28, Height = 28 };
        _thumbnailArea.Child = _colorDot;

        _name = new TextBlock { FontSize = 13, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 8, 0, 0) };
        _name.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextPrimary");
        _subtitle = new TextBlock { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        _subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextSecondary");

        Content = new StackPanel { Margin = new Thickness(10), Width = CardWidth, Children = { _thumbnailArea, _name, _subtitle } };
    }

    /// <summary>Muestra la vista previa de la pestaña en la posición indicada, sin activar la ventana.</summary>
    /// <param name="tab">Pestaña de la vista previa.</param>
    /// <param name="source">Ventana gestionada; <see langword="null"/> para mostrar solo la tarjeta.</param>
    /// <param name="sourceIsVisible">Solo una ventana visible tiene imagen que componer.</param>
    /// <param name="screenTopLeft">Esquina superior izquierda en coordenadas de pantalla (DIP).</param>
    public void ShowFor(TabViewModel tab, WindowHandle? source, bool sourceIsVisible, Point screenTopLeft)
    {
        ArgumentNullException.ThrowIfNull(tab);

        _name.Text = tab.DisplayName;
        _subtitle.Text = tab.Subtitle;
        _subtitle.Visibility = string.IsNullOrEmpty(tab.Subtitle) ? Visibility.Collapsed : Visibility.Visible;
        _colorDot.Fill = (Brush)s_brushes.Convert(tab.ColorHex, typeof(Brush), null, System.Globalization.CultureInfo.InvariantCulture);
        _colorDot.Visibility = Visibility.Visible;

        Left = screenTopLeft.X;
        Top = screenTopLeft.Y;
        Show();
        UpdateLayout();

        ReleaseThumbnail();
        if (source is { } handle && sourceIsVisible)
            TryShowThumbnail(handle);
    }

    public void HidePreview()
    {
        ReleaseThumbnail();
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        ReleaseThumbnail();
        base.OnClosed(e);
    }

    /// <summary>Registra la imagen en vivo dentro del área reservada, respetando la proporción de la ventana.</summary>
    private void TryShowThumbnail(WindowHandle source)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _thumbnail = LiveThumbnail.Create(hwnd, source);
        if (_thumbnail is null)
            return;

        var (width, height) = _thumbnail.SourceSize;
        if (width <= 0 || height <= 0)
        {
            ReleaseThumbnail();
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var origin = _thumbnailArea.TranslatePoint(new Point(0, 0), this);
        var areaWidth = _thumbnailArea.ActualWidth * dpi.DpiScaleX;
        var areaHeight = _thumbnailArea.ActualHeight * dpi.DpiScaleY;
        var scale = Math.Min(areaWidth / width, areaHeight / height);
        var w = width * scale;
        var h = height * scale;
        var left = (origin.X * dpi.DpiScaleX) + ((areaWidth - w) / 2);
        var top = (origin.Y * dpi.DpiScaleY) + ((areaHeight - h) / 2);

        if (_thumbnail.Show(new PixelRect((int)left, (int)top, (int)(left + w), (int)(top + h))))
            _colorDot.Visibility = Visibility.Collapsed;
        else
            ReleaseThumbnail();
    }

    private void ReleaseThumbnail()
    {
        _thumbnail?.Dispose();
        _thumbnail = null;
    }
}
