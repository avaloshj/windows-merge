using System.Globalization;
using System.Xml.Linq;

namespace TabDock.App.Tests.Themes;

/// <summary>
/// Contraste WCAG 2.1 AA (≥ 4.5:1 para texto normal) de los pares texto/fondo de los temas claro y oscuro.
/// Lee los colores directamente de Light.xaml y Dark.xaml; los fondos translúcidos se componen sobre su base.
/// Con Mica la barra lateral es translúcida y su color real depende del fondo de escritorio: no se puede verificar aquí.
/// </summary>
public sealed class ContrastTests
{
    private const double MinimumRatio = 4.5;

    public static TheoryData<string, string, string, string?> Pairs()
    {
        var data = new TheoryData<string, string, string, string?>();
        foreach (var theme in new[] { "Light", "Dark" })
        {
            foreach (var text in new[] { "Color.TextPrimary", "Color.TextSecondary" })
            {
                data.Add(theme, text, "Color.BgWindow", null);
                data.Add(theme, text, "Color.BgSidebar", null);
                data.Add(theme, text, "Color.TabActive", null);
                data.Add(theme, text, "Color.BgSidebar", "Color.TabHover");
                data.Add(theme, text, "Color.BgSidebar", "Color.SearchBackground");
            }

            data.Add(theme, "Color.BadgeText", "Color.BadgeBackground", null);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Text_meets_wcag_aa(string theme, string textKey, string backgroundKey, string? overlayKey)
    {
        var colors = LoadColors(theme);
        var background = colors[backgroundKey];
        if (overlayKey is not null)
            background = Composite(colors[overlayKey], background);

        var ratio = ContrastRatio(colors[textKey], background);

        ratio.ShouldBeGreaterThanOrEqualTo(MinimumRatio, $"{theme}: {textKey} sobre {backgroundKey}{(overlayKey is null ? string.Empty : " + " + overlayKey)} = {ratio:F2}:1");
    }

    [Fact]
    public void Both_themes_define_the_same_color_keys()
    {
        LoadColors("Dark").Keys.Order(StringComparer.Ordinal).ShouldBe(LoadColors("Light").Keys.Order(StringComparer.Ordinal));
    }

    private static Dictionary<string, Argb> LoadColors(string theme)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Themes", theme + ".xaml");
        return XDocument.Load(path).Root!
            .Elements()
            .Where(e => string.Equals(e.Name.LocalName, "Color", StringComparison.Ordinal))
            .ToDictionary(e => (string)e.Attribute(x + "Key")!, e => Argb.Parse(e.Value.Trim()), StringComparer.Ordinal);
    }

    private static Argb Composite(Argb top, Argb bottom)
    {
        var alpha = top.A / 255d;
        byte Mix(byte t, byte b) => (byte)Math.Round((t * alpha) + (b * (1 - alpha)));
        return new Argb(255, Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));
    }

    private static double ContrastRatio(Argb a, Argb b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double RelativeLuminance(Argb color)
    {
        static double Channel(byte value)
        {
            var c = value / 255d;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    private readonly record struct Argb(byte A, byte R, byte G, byte B)
    {
        /// <summary>Acepta <c>#RRGGBB</c> y <c>#AARRGGBB</c>.</summary>
        public static Argb Parse(string hex)
        {
            var value = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return hex.Length == 7
                ? new Argb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value)
                : new Argb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }
    }
}
