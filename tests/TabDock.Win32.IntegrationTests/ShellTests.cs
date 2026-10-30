using Microsoft.Win32;
using TabDock.Win32.Shell;

namespace TabDock.Win32.IntegrationTests;

/// <summary>Usa una clave propia bajo HKCU: nunca toca la clave <c>Run</c> real del usuario.</summary>
[Trait("Category", "Integration")]
public sealed class ShellTests : IDisposable
{
    private const string ParentKey = @"Software\TabDock.Tests";

    private readonly string _keyPath = ParentKey + @"\" + Guid.NewGuid().ToString("N");

    /// <summary>Borra la clave de la prueba y, si ya no queda ninguna, también la clave padre.</summary>
    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        bool empty;
        using (var parent = Registry.CurrentUser.OpenSubKey(ParentKey))
            empty = parent is not null && parent.SubKeyCount == 0 && parent.ValueCount == 0;

        if (empty)
            Registry.CurrentUser.DeleteSubKey(ParentKey, throwOnMissingSubKey: false);
    }

    [Fact]
    public void Start_with_windows_writes_and_removes_the_quoted_executable_path()
    {
        var startup = new StartupRegistration(@"C:\Programas de prueba\TabDock\TabDock.exe", _keyPath);
        startup.IsEnabled().ShouldBeFalse();

        startup.SetEnabled(true).IsSuccess.ShouldBeTrue();
        startup.IsEnabled().ShouldBeTrue();
        using (var key = Registry.CurrentUser.OpenSubKey(_keyPath)!)
            key.GetValue(StartupRegistration.DefaultValueName).ShouldBe("\"C:\\Programas de prueba\\TabDock\\TabDock.exe\"");

        startup.SetEnabled(false).IsSuccess.ShouldBeTrue();
        startup.IsEnabled().ShouldBeFalse();
        startup.SetEnabled(false).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void An_entry_pointing_to_another_copy_counts_as_disabled()
    {
        new StartupRegistration(@"C:\Antigua\TabDock.exe", _keyPath).SetEnabled(true);

        new StartupRegistration(@"C:\Nueva\TabDock.exe", _keyPath).IsEnabled().ShouldBeFalse();
    }

    [Fact]
    public void Displays_are_listed_with_their_scale()
    {
        var displays = Displays.Enumerate();

        displays.ShouldNotBeEmpty();
        displays.Count(d => d.IsPrimary).ShouldBe(1);
        displays.ShouldAllBe(d => d.Width > 0 && d.Height > 0 && d.Dpi >= 96);
    }
}
