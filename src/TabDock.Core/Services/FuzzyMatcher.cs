using System.Globalization;
using System.Text;

namespace TabDock.Core.Services;

/// <summary>
/// Coincidencia difusa para la búsqueda rápida: las letras de la consulta deben aparecer en orden, no necesariamente
/// seguidas. Puntúa más los inicios de palabra, las letras consecutivas y el comienzo del texto. Ignora mayúsculas y
/// acentos.
/// </summary>
public static class FuzzyMatcher
{
    private const int MatchScore = 1;
    private const int ConsecutiveBonus = 5;
    private const int WordStartBonus = 8;
    private const int TextStartBonus = 10;

    /// <summary>Devuelve la puntuación (mayor es mejor) o <see langword="null"/> si no coincide.</summary>
    public static int? Score(string? query, string? text)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 0;

        if (string.IsNullOrEmpty(text))
            return null;

        var q = Simplify(query.Trim());
        var t = Simplify(text);
        var score = 0;
        var position = 0;
        var previousMatch = -2;
        foreach (var c in q)
        {
            if (char.IsWhiteSpace(c))
                continue;

            var found = t.IndexOf(c, position);
            if (found < 0)
                return null;

            score += MatchScore;
            if (found == previousMatch + 1)
                score += ConsecutiveBonus;
            if (found == 0)
                score += TextStartBonus;
            else if (!char.IsLetterOrDigit(t[found - 1]))
                score += WordStartBonus;

            previousMatch = found;
            position = found + 1;
        }

        return score;
    }

    /// <summary>Minúsculas sin diacríticos (buscar "indice" encuentra "índice").</summary>
    private static string Simplify(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString();
    }
}
