using System.ComponentModel.DataAnnotations;

namespace Jezda.Common.Abstractions.Configuration.Options;

/// <summary>
/// JWT authentication configuration options
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// JWT token issuer (iss claim)
    /// </summary>
    [Required(ErrorMessage = "JWT Issuer is required")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// JWT token audience (aud claim)
    /// </summary>
    [Required(ErrorMessage = "JWT Audience is required")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Secret key for signing JWT tokens (minimum 32 characters recommended)
    /// </summary>
    [Required(ErrorMessage = "JWT Key is required")]
    [MinLength(32, ErrorMessage = "JWT Key must be at least 32 characters long")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Access token expiration time in minutes
    /// </summary>
    [Range(1, 1440, ErrorMessage = "Token expiration must be between 1 and 1440 minutes")]
    public int TokenExpirationInMinutes { get; set; } = 30;

    /// <summary>
    /// Refresh token expiration time in days (how long until a refresh token can no longer be used)
    /// </summary>
    [Range(1, 365, ErrorMessage = "Refresh token expiration must be between 1 and 365 days")]
    public int RefreshTokenExpirationInDays { get; set; } = 7;

    /// <summary>
    /// Refresh token time-to-live in days (how long a refresh token stays in the database before cleanup)
    /// </summary>
    [Range(1, 365, ErrorMessage = "Refresh token TTL must be between 1 and 365 days")]
    public int RefreshTokenTimeToLiveInDays { get; set; } = 30;
}
