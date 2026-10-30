using System.Text.Json;
using System.Text.Json.Serialization;
using TabDock.Core.Abstractions;

namespace TabDock.Infrastructure.Persistence;

/// <summary><see cref="WindowHandle"/> se guarda como número (System.Text.Json no serializa <see cref="nint"/>).</summary>
public sealed class WindowHandleJsonConverter : JsonConverter<WindowHandle>
{
    public override WindowHandle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new((nint)reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, WindowHandle value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue((long)value.Value);
    }
}
