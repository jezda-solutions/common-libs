using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Jezda.Common.Files.Naming;

public sealed class FileNamePolicy
{
    public int MaxFileNameLength { get; set; } = 255;
    public string AllowedPattern { get; set; } = "^[\\p{L}\\p{N}._\\- ]+$";
    public bool NormalizeUnicode { get; set; } = true;
    public bool ReplaceInvalidChars { get; set; } = true;
    public char ReplacementChar { get; set; } = '_';

    public (bool isValid, string normalized) ValidateAndNormalize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return (false, string.Empty);

        var name = fileName.Trim();
        if (NormalizeUnicode)
            name = name.Normalize(NormalizationForm.FormC);

        if (name.Length > MaxFileNameLength)
            name = name[..MaxFileNameLength];

        var regex = new Regex(AllowedPattern, RegexOptions.Compiled);
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
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
        };
        if (reserved.Contains(Path.GetFileNameWithoutExtension(name)))
            name = $"_{name}";

        return (true, name);
    }
}