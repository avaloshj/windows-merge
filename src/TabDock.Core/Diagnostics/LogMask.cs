using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TabDock.Core.Diagnostics;

/// <summary>Funciones de enmascaramiento para logs y diagnóstico.</summary>
public static partial class LogMask
{
    public const string UserProfileToken = "%USERPROFILE%";
    public const string UserNameToken = "%USERNAME%";

    /// <summary>Devuelve <c>[h:xxxxxxxx len:N]</c>: SHA-256 truncado a 8 caracteres hexadecimales y la longitud.</summary>
    public static string Hash(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "[empty]";

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var shortHash = Convert.ToHexStringLower(digest.AsSpan(0, 4));
        return string.Create(CultureInfo.InvariantCulture, $"[h:{shortHash} len:{value.Length}]");
    }

    /// <summary>Indica si el texto ya es el resultado de <see cref="Hash(string?)"/>.</summary>
    public static bool IsHashed(string? value) =>
        value is not null && (string.Equals(value, "[empty]", StringComparison.Ordinal) || HashPattern().IsMatch(value));

    /// <summary>
    /// Sustituye la carpeta de perfil por <c>%USERPROFILE%</c> y cualquier segmento de ruta igual al nombre de usuario
    /// por <c>%USERNAME%</c> (p. ej. <c>C:\ProgramData\nombre\app.exe</c>). No distingue mayúsculas.
    /// </summary>
    public static string NormalizeUserPath(string? value, MaskingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = value;
        var profile = context.UserProfile?.TrimEnd('\\', '/');
        if (!string.IsNullOrEmpty(profile))
            result = result.Replace(profile, UserProfileToken, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(context.UserName))
            result = ReplacePathSegment(result, context.UserName, UserNameToken);

        return result;
    }

    public static string Apply(SensitiveDataKind kind, string? value, MaskingContext context) => kind switch
    {
        SensitiveDataKind.UserPath => NormalizeUserPath(value, context),
        _ => Hash(value),
    };

    private static string ReplacePathSegment(string value, string segment, string token)
    {
        StringBuilder? builder = null;
        var copied = 0;
        var index = value.IndexOf(segment, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var end = index + segment.Length;
            var isWholeSegment = IsSeparatorOrEdge(value, index - 1) && IsSeparatorOrEdge(value, end);
            if (isWholeSegment)
            {
                builder ??= new StringBuilder(value.Length);
                builder.Append(value, copied, index - copied).Append(token);
                copied = end;
            }

            var next = isWholeSegment ? end : index + 1;
            index = next < value.Length ? value.IndexOf(segment, next, StringComparison.OrdinalIgnoreCase) : -1;
        }

        return builder is null ? value : builder.Append(value, copied, value.Length - copied).ToString();
    }

    private static bool IsSeparatorOrEdge(string value, int position) =>
        position < 0 || position >= value.Length || value[position] is '\\' or '/';

    [GeneratedRegex(@"^\[h:[0-9a-f]{8} len:\d+\]$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex HashPattern();
}
