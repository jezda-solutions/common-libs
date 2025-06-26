namespace Jezda.Common.Abstractions.Configuration.Options;

public class JwtOptions
{
    public string Key { get; set; } = default!;

    public string Issuer { get; set; } = default!;

    public string Audience { get; set; } = default!;

    public double TokenExpirationInMinutes { get; set; }

    public int RefreshTokenExpirationInDays { get; set; }

    public decimal RefreshTokenTimeToLiveInDays { get; set; }
}
