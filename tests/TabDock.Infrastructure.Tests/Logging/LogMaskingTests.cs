using System.Globalization;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using TabDock.Core.Abstractions;
using TabDock.Core.Diagnostics;
using TabDock.Infrastructure.Logging;

namespace TabDock.Infrastructure.Tests.Logging;

/// <summary>Verifica que ningún dato sensible ficticio llega al JSON que se escribe en disco.</summary>
public sealed class LogMaskingTests : IDisposable
{
    private const string FakeProfile = @"C:\Users\usuario-ficticio";
    private const string FakeTitle = "● main.ts - cliente-ficticio-sa - Visual Studio Code";

    private static readonly MaskingContext s_context = new(FakeProfile, "usuario-ficticio");

    private readonly CollectingSink _sink = new();
    private readonly Logger _logger;

    public LogMaskingTests()
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WithTabDockMasking(s_context)
            .WriteTo.Sink(_sink)
            .CreateLogger();
    }

    public void Dispose() => _logger.Dispose();

    [Fact]
    public void Destructured_window_masks_title_and_normalizes_executable_path()
    {
        _logger.Information("Ventana {@Window}", CreateWindow());

        var json = RenderSingle();
        json.ShouldNotContain("cliente-ficticio");
        json.ShouldNotContain("usuario-ficticio");
        json.ShouldContain(LogMask.Hash(FakeTitle));
        json.ShouldContain("%USERPROFILE%");
        json.ShouldContain("Chrome_WidgetWin_1");
    }

    [Fact]
    public void Masked_title_is_not_hashed_twice()
    {
        _logger.Information("Ventana {@Window}", CreateWindow());

        var window = (StructureValue)_sink.Events.Single().Properties["Window"];
        var title = (ScalarValue)window.Properties.Single(p => string.Equals(p.Name, nameof(WindowInfo.Title), StringComparison.Ordinal)).Value;
        title.Value.ShouldBe(LogMask.Hash(FakeTitle));
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("ProjectName")]
    [InlineData("DisplayName")]
    [InlineData("Alias")]
    [InlineData("FilePath")]
    public void Scalar_property_with_sensitive_name_is_hashed(string propertyName)
    {
        _logger.Information($"Valor {{{propertyName}}}", "proyecto-confidencial-ficticio");

        var json = RenderSingle();
        json.ShouldNotContain("proyecto-confidencial-ficticio");
        json.ShouldContain(LogMask.Hash("proyecto-confidencial-ficticio"));
    }

    [Fact]
    public void Any_string_containing_the_user_profile_is_normalized()
    {
        _logger.Information("Ruta {Location}", FakeProfile + @"\AppData\Roaming\TabDock");

        var json = RenderSingle();
        json.ShouldNotContain("usuario-ficticio");
        json.ShouldContain(@"%USERPROFILE%\\AppData\\Roaming\\TabDock");
    }

    [Fact]
    public void User_name_outside_the_profile_folder_is_masked()
    {
        _logger.Information("Ejecutable {Location}", @"C:\ProgramData\usuario-ficticio\AppFicticia\app.exe");

        var json = RenderSingle();
        json.ShouldNotContain("usuario-ficticio");
        json.ShouldContain(@"C:\\ProgramData\\%USERNAME%\\AppFicticia\\app.exe");
    }

    [Fact]
    public void Nested_anonymous_objects_are_masked()
    {
        _logger.Information("Datos {@Data}", new { ProjectName = "proyecto-beta", Tabs = new[] { new { Title = "demo-api" } }, Count = 3 });

        var json = RenderSingle();
        json.ShouldNotContain("proyecto-beta");
        json.ShouldNotContain("demo-api");
        json.ShouldContain("\"Count\":3");
    }

    [Fact]
    public void Dictionary_entries_with_sensitive_keys_are_masked()
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["Title"] = "proyecto-gamma", ["Mode"] = "hide" };

        _logger.Information("Datos {@Data}", data);

        var json = RenderSingle();
        json.ShouldNotContain("proyecto-gamma");
        json.ShouldContain("hide");
    }

    [Fact]
    public void Rendered_message_does_not_leak_sensitive_values()
    {
        _logger.Information("Pestaña {Title} en {Location}", "proyecto-delta", FakeProfile + @"\x");

        var message = _sink.Events.Single().RenderMessage(CultureInfo.InvariantCulture);
        message.ShouldNotContain("proyecto-delta");
        message.ShouldNotContain("usuario-ficticio");
    }

    [Fact]
    public void Non_sensitive_values_are_kept_intact()
    {
        _logger.Information("Enumeradas {Total} ventanas en {ElapsedMs} ms", 12, 4L);

        var json = RenderSingle();
        json.ShouldContain("\"Total\":12");
        json.ShouldContain("\"ElapsedMs\":4");
    }

    [Fact]
    public void Production_formatter_masks_paths_inside_exceptions()
    {
        var exception = new FileNotFoundException(
            $"No se encontró {FakeProfile}\\proyectos\\cliente\\archivo.json",
            @"C:\ProgramData\usuario-ficticio\cache.bin");

        _logger.Error(exception, "Fallo de lectura");

        var json = RenderWithProductionFormatter();
        json.ShouldContain("\"@x\"");
        json.ShouldNotContain("usuario-ficticio");
        json.ShouldContain(@"%USERPROFILE%\\proyectos");
    }

    [Fact]
    public void Production_formatter_keeps_unrelated_text()
    {
        _logger.Information("Ruta {Location}", @"C:\Program Files\EditorFicticio\Code.exe");

        RenderWithProductionFormatter().ShouldContain(@"C:\\Program Files\\EditorFicticio\\Code.exe");
    }

    private static WindowInfo CreateWindow() => new()
    {
        Handle = new WindowHandle(0xABC),
        ProcessId = 1000,
        ExecutablePath = FakeProfile + @"\AppData\Local\Programs\EditorFicticio\Code.exe",
        ClassName = "Chrome_WidgetWin_1",
        Title = FakeTitle,
        ExtendedFrameBounds = new PixelRect(0, 0, 1280, 800),
        IsVisible = true,
        IsCloaked = false,
        HasOwner = false,
        IsToolWindow = false,
        IsProcessAccessible = true,
        IsElevatedProcess = false,
        ShowState = WindowShowState.Normal,
    };

    /// <summary>JSON sin la normalización final: verifica el enmascaramiento de propiedades por sí solo.</summary>
    private string RenderSingle()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new CompactJsonFormatter().Format(_sink.Events.Single(), writer);
        return writer.ToString();
    }

    /// <summary>JSON exactamente como se escribe en disco.</summary>
    private string RenderWithProductionFormatter()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        TabDockLogging.CreateFormatter(s_context).Format(_sink.Events.Single(), writer);
        return writer.ToString();
    }
}
