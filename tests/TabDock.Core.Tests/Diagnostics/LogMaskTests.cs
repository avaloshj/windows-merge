using TabDock.Core.Diagnostics;

namespace TabDock.Core.Tests.Diagnostics;

public sealed class LogMaskTests
{
    private const string FakeProfile = @"C:\Users\usuario-ficticio";

    private static readonly MaskingContext s_context = new(FakeProfile, "usuario-ficticio");

    [Fact]
    public void Hash_uses_truncated_sha256_and_length()
    {
        // SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        LogMask.Hash("abc").ShouldBe("[h:ba7816bf len:3]");
    }

    [Fact]
    public void Hash_is_deterministic_and_never_contains_the_input()
    {
        const string title = "● index.ts - cliente-ficticio-sa - Visual Studio Code";

        var first = LogMask.Hash(title);

        first.ShouldBe(LogMask.Hash(title));
        first.ShouldNotContain("cliente-ficticio");
        first.ShouldEndWith($"len:{title.Length}]");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hash_of_missing_value_is_marked_empty(string? value)
    {
        LogMask.Hash(value).ShouldBe("[empty]");
    }

    [Fact]
    public void IsHashed_recognises_only_masked_values()
    {
        LogMask.IsHashed(LogMask.Hash("proyecto-alfa")).ShouldBeTrue();
        LogMask.IsHashed("[empty]").ShouldBeTrue();
        LogMask.IsHashed("proyecto-alfa").ShouldBeFalse();
        LogMask.IsHashed("[h:XYZ len:3]").ShouldBeFalse();
        LogMask.IsHashed(null).ShouldBeFalse();
    }

    [Theory]
    [InlineData(@"C:\Users\usuario-ficticio\AppData\Local\Programs\Editor\Code.exe", @"%USERPROFILE%\AppData\Local\Programs\Editor\Code.exe")]
    [InlineData(@"c:\users\USUARIO-FICTICIO\proyectos", @"%USERPROFILE%\proyectos")]
    [InlineData(@"C:\Program Files\Editor\Code.exe", @"C:\Program Files\Editor\Code.exe")]
    public void NormalizeUserPath_replaces_profile_case_insensitively(string input, string expected)
    {
        LogMask.NormalizeUserPath(input, s_context).ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"C:\ProgramData\usuario-ficticio\App\app.exe", @"C:\ProgramData\%USERNAME%\App\app.exe")]
    [InlineData(@"D:\USUARIO-FICTICIO", @"D:\%USERNAME%")]
    [InlineData(@"\\servidor-ficticio\usuario-ficticio\x", @"\\servidor-ficticio\%USERNAME%\x")]
    [InlineData(@"C:/datos/usuario-ficticio/x", @"C:/datos/%USERNAME%/x")]
    [InlineData(@"C:\usuario-ficticio-2\usuario-ficticio\x", @"C:\usuario-ficticio-2\%USERNAME%\x")]
    [InlineData(@"C:\otro-usuario-ficticio\x", @"C:\otro-usuario-ficticio\x")]
    public void NormalizeUserPath_masks_user_name_only_as_a_whole_path_segment(string input, string expected)
    {
        LogMask.NormalizeUserPath(input, s_context).ShouldBe(expected);
    }

    [Fact]
    public void NormalizeUserPath_ignores_trailing_separator_in_profile()
    {
        var context = s_context with { UserProfile = FakeProfile + @"\" };

        LogMask.NormalizeUserPath(@"C:\Users\usuario-ficticio\x", context).ShouldBe(@"%USERPROFILE%\x");
    }

    [Fact]
    public void NormalizeUserPath_without_context_returns_input()
    {
        LogMask.NormalizeUserPath(@"C:\Users\usuario-ficticio\x", MaskingContext.None).ShouldBe(@"C:\Users\usuario-ficticio\x");
    }

    [Fact]
    public void Apply_dispatches_by_kind()
    {
        LogMask.Apply(SensitiveDataKind.Text, "proyecto-beta", s_context).ShouldBe(LogMask.Hash("proyecto-beta"));
        LogMask.Apply(SensitiveDataKind.UserPath, FakeProfile + @"\a.exe", s_context).ShouldBe(@"%USERPROFILE%\a.exe");
    }
}
