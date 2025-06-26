namespace Jezda.Common.Helpers;

public static class DisplayMasker
{
    /// <summary>
    /// Masks a string by replacing all but the last n characters with a specified character.
    /// Result looks like this: "**** **** **** 1234"
    /// </summary>
    /// <param name="value"></param>
    /// <param name="visibleSuffixLength"></param>
    /// <param name="maskChar"></param>
    /// <returns></returns>
    public static string Mask(string value, int visibleSuffixLength = 4, char maskChar = '*')
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();
        int maskLength = Math.Max(0, value.Length - visibleSuffixLength);
        string maskedPart = new(maskChar, maskLength);
        string visiblePart = value[^(Math.Min(visibleSuffixLength, value.Length))..];

        string combined = maskedPart + visiblePart;
        return string.Join(" ", Enumerable.Range(0, (int)Math.Ceiling(combined.Length / 4.0))
            .Select(i => combined.Substring(i * 4, Math.Min(4, combined.Length - i * 4))));
    }
}
