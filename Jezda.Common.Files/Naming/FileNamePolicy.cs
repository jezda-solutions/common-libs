using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Jezda.Common.Files.Naming;

/// <summary>
/// Defines a policy for validating and normalizing file names with Unicode support.
/// Handles invalid characters, reserved names, Unicode normalization, and length constraints.
/// </summary>
public sealed class FileNamePolicy
{
    /// <summary>
    /// Gets or sets the maximum allowed file name length. Default is 255 characters.
    /// </summary>
    public int MaxFileNameLength { get; set; } = 255;

    /// <summary>
    /// Gets or sets the regex pattern for allowed characters.
    /// Default allows Unicode letters, digits, dots, underscores, hyphens, and spaces.
    /// </summary>
    public string AllowedPattern { get; set; } = "^[\\p{L}\\p{N}._\\- ]+$";

    /// <summary>
    /// Gets or sets whether to normalize Unicode characters to Form C. Default is true.
    /// </summary>
    public bool NormalizeUnicode { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to replace invalid characters with the replacement character.
    /// If false, validation fails on invalid characters. Default is true.
    /// </summary>
    public bool ReplaceInvalidChars { get; set; } = true;

    /// <summary>
    /// Gets or sets the character used to replace invalid characters. Default is underscore '_'.
    /// </summary>
    public char ReplacementChar { get; set; } = '_';

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
    };

    // One compiled regex per policy, rebuilt only when AllowedPattern changes; the pair is
    // swapped as a single reference so concurrent calls never see a pattern with another's regex.
    private (string Pattern, Regex Regex)? _cachedRegex;

    private Regex GetRegex()
    {
        var pattern = AllowedPattern;
        var cached = _cachedRegex;
        if (cached is { } c && c.Pattern == pattern)
            return c.Regex;

        var regex = new Regex(pattern, RegexOptions.Compiled);
        _cachedRegex = (pattern, regex);
        return regex;
    }

    /// <summary>
    /// Validates and normalizes a file name according to the policy rules.
    /// </summary>
    /// <param name="fileName">The file name to validate and normalize.</param>
    /// <returns>A tuple indicating whether the name is valid and the normalized file name.</returns>
    public (bool isValid, string normalized) ValidateAndNormalize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return (false, string.Empty);

        var name = fileName.Trim();
        if (NormalizeUnicode)
            name = name.Normalize(NormalizationForm.FormC);

        if (name.Length > MaxFileNameLength)
            name = name[..MaxFileNameLength];

        var regex = GetRegex();
        if (!regex.IsMatch(name))
        {
            if (!ReplaceInvalidChars)
                return (false, name);

            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (regex.IsMatch(ch.ToString())) sb.Append(ch);
                else sb.Append(ReplacementChar);
            }
            name = sb.ToString();
        }

        // Avoid special/reserved names
        if (ReservedNames.Contains(Path.GetFileNameWithoutExtension(name)))
            name = $"_{name}";

        return (true, name);
    }
}