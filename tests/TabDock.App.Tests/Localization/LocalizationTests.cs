using System.Collections;
using System.Globalization;
using TabDock.App.Localization;
using TabDock.Core.Services;

namespace TabDock.App.Tests.Localization;

public sealed class LocalizationTests
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo("en");

    [Fact]
    public void Spanish_and_english_define_the_same_keys()
    {
        var spanish = ReadResources(CultureInfo.InvariantCulture);
        var english = ReadResources(s_english);

        english.Keys.Order(StringComparer.Ordinal).ShouldBe(spanish.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void No_resource_is_empty()
    {
        foreach (var (key, value) in ReadResources(CultureInfo.InvariantCulture).Concat(ReadResources(s_english)))
            value.ShouldNotBeNullOrWhiteSpace($"El recurso '{key}' está vacío.");
    }

    [Fact]
    public void Every_eligibility_status_and_reason_has_a_text()
    {
        var keys = ReadResources(CultureInfo.InvariantCulture).Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var status in Enum.GetNames<EligibilityStatus>())
            keys.Contains("Status_" + status).ShouldBeTrue($"Falta Status_{status}.");

        foreach (var reason in Enum.GetNames<ExclusionReason>())
            keys.Contains("Reason_" + reason).ShouldBeTrue($"Falta Reason_{reason}.");
    }

    [Fact]
    public void Missing_key_is_rendered_visibly()
    {
        TextResources.Get("Clave_Inexistente").ShouldBe("[Clave_Inexistente]");
    }

    /// <summary>Lee solo los recursos de esa cultura (sin recurrir a la neutra).</summary>
    private static Dictionary<string, string?> ReadResources(CultureInfo culture)
    {
        var set = TextResources.Manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        set.ShouldNotBeNull($"No hay recursos para la cultura '{culture.Name}'.");

        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value as string, StringComparer.Ordinal);
    }
}
