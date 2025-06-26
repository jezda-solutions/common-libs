using Jezda.Common.Domain.Enums;

namespace Jezda.Common.Helpers;

/// <summary>
/// Helper class for converting currency codes to their respective symbols.
/// </summary>
public static class CurrencyCodeHelper
{
    public static string GetCurrencySymbol(CurrencyCode code)
    {
        return code switch
        {
            CurrencyCode.EUR => "€",
            CurrencyCode.USD => "$",
            CurrencyCode.RSD => "RSD",
            CurrencyCode.CHF => "CHF",
            CurrencyCode.GBP => "£",
            CurrencyCode.JPY => "¥",
            _ => code.ToString()
        };
    }
}
