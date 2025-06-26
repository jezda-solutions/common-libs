using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jezda.Common.Helpers;

/// <summary>
/// A collection of static methods for working with strings, including parsing, formatting, 
/// comparison, and similarity calculation. This class provides a set of reusable utilities 
/// for common string manipulation tasks.
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// Calculates the similarity between two strings using the Levenshtein distance algorithm.
    /// 
    /// The similarity is calculated as 1 minus the ratio of the Levenshtein distance to the maximum length of the two strings.
    /// 
    /// Args:
    ///     source (string): The source string.
    ///     target (string): The target string.
    /// 
    /// Returns:
    ///     double: A value between 0 and 1 representing the similarity between the two strings.
    /// </summary>
    public static double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            return 0;

        int distance = LevenshteinDistance(source, target);
        int maxLength = Math.Max(source.Length, target.Length);

        return 1.0 - (double)distance / maxLength;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int[,] dp = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++)
            dp[i, 0] = i;
        for (int j = 0; j <= t.Length; j++)
            dp[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;

                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }

        return dp[s.Length, t.Length];
    }

    /// <summary>
    /// Normalizes a string by converting Cyrillic characters to Latin, removing diacritics,
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string NormalizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Konverzija ćirilice u latinicu
        input = ConvertCyrillicToLatin(input);

        // Normalizacija latiničnih karaktera
        input = RemoveDiacritics(input);

        // Uklanjanje specijalnih karaktera i razmaka
        input = new string([.. input.Where(c => char.IsLetterOrDigit(c))]);

        return input.ToLower();
    }

    private static string ConvertCyrillicToLatin(string text)
    {
        return text
            .Replace("А", "A").Replace("Б", "B").Replace("В", "V").Replace("Г", "G").Replace("Д", "D")
            .Replace("Е", "E").Replace("Ж", "Z").Replace("З", "Z").Replace("И", "I").Replace("Ј", "J")
            .Replace("К", "K").Replace("Л", "L").Replace("М", "M").Replace("Н", "N").Replace("О", "O")
            .Replace("П", "P").Replace("Р", "R").Replace("С", "S").Replace("Т", "T").Replace("У", "U")
            .Replace("Ф", "F").Replace("Х", "H").Replace("Ц", "C").Replace("Ч", "C").Replace("Џ", "D")
            .Replace("Ш", "S").Replace("Ђ", "Dj").Replace("Љ", "Lj").Replace("Њ", "Nj")
            .Replace("Ћ", "C");
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString()
            .Replace("đ", "dj")
            .Replace("Đ", "Dj")
            .Replace("č", "c").Replace("Č", "C")
            .Replace("ć", "c").Replace("Ć", "C")
            .Replace("š", "s").Replace("Š", "S")
            .Replace("ž", "z").Replace("Ž", "Z");
    }
}
